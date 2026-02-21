//! Full-text search indexing with Tantivy for the Knowledge Exchange Platform.
//!
//! This module provides:
//! - Full-text search over notebook entries
//! - Notebook-scoped search filtering
//! - Match snippet generation with highlighting
//!
//! ## Example Usage
//!
//! ```rust,ignore
//! use notebook_entropy::search::{SearchIndex, SearchHit};
//! use notebook_core::types::{Entry, NotebookId, EntryId};
//! use std::path::Path;
//!
//! // Create or open an index
//! let index = SearchIndex::new(Path::new("./data/search_index")).unwrap();
//!
//! // Index an entry
//! let notebook_id = NotebookId::new();
//! index.index_entry(notebook_id, &entry).unwrap();
//!
//! // Search within a notebook
//! let hits = index.search("knowledge exchange", notebook_id, 10).unwrap();
//! for hit in hits {
//!     println!("Entry: {} Score: {} Snippet: {}", hit.entry_id, hit.score, hit.snippet);
//! }
//! ```
//!
//! Owned by: agent-search (Task 3-2)

use std::path::Path;
use std::sync::{Arc, Mutex};

use serde::{Deserialize, Serialize};
use tantivy::collector::TopDocs;
use tantivy::query::{BooleanQuery, Occur, QueryParser, TermQuery};
use tantivy::schema::document::Value;
use tantivy::schema::{
    Field, IndexRecordOption, STORED, STRING, Schema, TextFieldIndexing, TextOptions,
};
use tantivy::{Index, IndexReader, IndexWriter, ReloadPolicy, Term, doc};
use thiserror::Error;

use notebook_core::types::{Entry, EntryId, NotebookId};

/// Maximum snippet length in characters.
const MAX_SNIPPET_LENGTH: usize = 200;

/// Default heap size for the index writer (50 MB).
const WRITER_HEAP_SIZE: usize = 50_000_000;

/// Errors that can occur during search operations.
#[derive(Error, Debug)]
pub enum SearchError {
    /// Failed to create or open the index.
    #[error("index error: {0}")]
    IndexError(String),

    /// Failed to parse the search query.
    #[error("query parse error: {0}")]
    QueryParseError(String),

    /// Failed to execute the search.
    #[error("search error: {0}")]
    SearchExecutionError(String),

    /// Failed to index or delete a document.
    #[error("indexing error: {0}")]
    IndexingError(String),

    /// Internal lock error.
    #[error("internal lock error")]
    LockError,
}

impl<T> From<std::sync::PoisonError<T>> for SearchError {
    fn from(_: std::sync::PoisonError<T>) -> Self {
        SearchError::LockError
    }
}

/// A search result with relevance score and match snippet.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SearchHit {
    /// The ID of the matching entry.
    pub entry_id: EntryId,

    /// Relevance score (higher is more relevant).
    pub score: f32,

    /// Snippet of content with match context.
    pub snippet: String,
}

/// Schema field indices for the search index.
#[derive(Clone)]
struct SearchFields {
    entry_id: Field,
    notebook_id: Field,
    content: Field,
    topic: Field,
    author_id: Field,
    content_type: Field,
}

/// Full-text search index for notebook entries.
///
/// Uses Tantivy for fast, Rust-native full-text search. The index stores
/// all notebooks in a single index, with notebook_id filtering at query time.
pub struct SearchIndex {
    /// Tantivy Index must stay alive for RAII (keeps directory lock and segment files open).
    #[allow(dead_code)]
    index: Index,
    reader: IndexReader,
    writer: Arc<Mutex<IndexWriter>>,
    fields: SearchFields,
    query_parser: QueryParser,
}

impl SearchIndex {
    /// Creates a new search index at the specified path, or opens an existing one.
    ///
    /// # Arguments
    ///
    /// * `index_path` - Path to the directory where the index will be stored.
    ///
    /// # Errors
    ///
    /// Returns `SearchError::IndexError` if the index cannot be created or opened.
    pub fn new(index_path: &Path) -> Result<Self, SearchError> {
        // Build the schema
        let (schema, fields) = Self::build_schema();

        // Create directory if it doesn't exist
        std::fs::create_dir_all(index_path).map_err(|e| {
            SearchError::IndexError(format!("failed to create index directory: {}", e))
        })?;

        // Open or create the index
        let index = Index::open_or_create(
            tantivy::directory::MmapDirectory::open(index_path)
                .map_err(|e| SearchError::IndexError(format!("failed to open directory: {}", e)))?,
            schema.clone(),
        )
        .map_err(|e| SearchError::IndexError(format!("failed to open/create index: {}", e)))?;

        // Create the writer
        let writer = index
            .writer(WRITER_HEAP_SIZE)
            .map_err(|e| SearchError::IndexError(format!("failed to create writer: {}", e)))?;

        // Create the reader with auto-reload
        let reader = index
            .reader_builder()
            .reload_policy(ReloadPolicy::OnCommitWithDelay)
            .try_into()
            .map_err(|e| SearchError::IndexError(format!("failed to create reader: {}", e)))?;

        // Create the query parser for content and topic fields
        let query_parser = QueryParser::for_index(&index, vec![fields.content, fields.topic]);

        Ok(Self {
            index,
            reader,
            writer: Arc::new(Mutex::new(writer)),
            fields,
            query_parser,
        })
    }

    /// Builds the Tantivy schema for the search index.
    fn build_schema() -> (Schema, SearchFields) {
        let mut schema_builder = Schema::builder();

        // entry_id: stored for retrieval (STRING = not tokenized)
        let entry_id = schema_builder.add_text_field("entry_id", STRING | STORED);

        // notebook_id: indexed for filtering (STRING = not tokenized)
        let notebook_id = schema_builder.add_text_field("notebook_id", STRING);

        // content: tokenized and stored for snippets
        let text_indexing = TextFieldIndexing::default()
            .set_tokenizer("default")
            .set_index_option(IndexRecordOption::WithFreqsAndPositions);
        let text_options = TextOptions::default()
            .set_indexing_options(text_indexing)
            .set_stored();
        let content = schema_builder.add_text_field("content", text_options.clone());

        // topic: tokenized for search
        let topic_options = TextOptions::default().set_indexing_options(
            TextFieldIndexing::default()
                .set_tokenizer("default")
                .set_index_option(IndexRecordOption::WithFreqsAndPositions),
        );
        let topic = schema_builder.add_text_field("topic", topic_options);

        // author_id: indexed for filtering (STRING = not tokenized)
        let author_id = schema_builder.add_text_field("author_id", STRING);

        // content_type: indexed for filtering (STRING = not tokenized)
        let content_type = schema_builder.add_text_field("content_type", STRING);

        let schema = schema_builder.build();

        let fields = SearchFields {
            entry_id,
            notebook_id,
            content,
            topic,
            author_id,
            content_type,
        };

        (schema, fields)
    }

    /// Indexes an entry for full-text search.
    ///
    /// If an entry with the same ID already exists, it will be updated.
    ///
    /// # Arguments
    ///
    /// * `notebook_id` - The notebook containing the entry.
    /// * `entry` - The entry to index.
    ///
    /// # Errors
    ///
    /// Returns `SearchError::IndexingError` if the entry cannot be indexed.
    pub fn index_entry(&self, notebook_id: NotebookId, entry: &Entry) -> Result<(), SearchError> {
        let mut writer = self.writer.lock()?;

        // Delete any existing document with this entry_id
        let entry_id_str = entry.id.to_string();
        writer.delete_term(Term::from_field_text(self.fields.entry_id, &entry_id_str));

        // Extract content as string
        let content_str = String::from_utf8_lossy(&entry.content);

        // Extract topic (empty string if none)
        let topic_str = entry.topic.as_deref().unwrap_or("");

        // Build and add the document
        let doc = doc!(
            self.fields.entry_id => entry_id_str,
            self.fields.notebook_id => notebook_id.to_string(),
            self.fields.content => content_str.to_string(),
            self.fields.topic => topic_str,
            self.fields.author_id => entry.author.to_string(),
            self.fields.content_type => entry.content_type.clone(),
        );

        writer
            .add_document(doc)
            .map_err(|e| SearchError::IndexingError(format!("failed to add document: {}", e)))?;

        writer
            .commit()
            .map_err(|e| SearchError::IndexingError(format!("failed to commit: {}", e)))?;

        Ok(())
    }

    /// Searches for entries matching the query within a specific notebook.
    ///
    /// # Arguments
    ///
    /// * `query_str` - The search query string.
    /// * `notebook_id` - The notebook to search within.
    /// * `limit` - Maximum number of results to return.
    ///
    /// # Errors
    ///
    /// Returns `SearchError::QueryParseError` if the query cannot be parsed,
    /// or `SearchError::SearchExecutionError` if the search fails.
    pub fn search(
        &self,
        query_str: &str,
        notebook_id: NotebookId,
        limit: usize,
    ) -> Result<Vec<SearchHit>, SearchError> {
        let searcher = self.reader.searcher();

        // Parse the text query
        let text_query = self
            .query_parser
            .parse_query(query_str)
            .map_err(|e| SearchError::QueryParseError(format!("failed to parse query: {}", e)))?;

        // Create notebook filter
        let notebook_term =
            Term::from_field_text(self.fields.notebook_id, &notebook_id.to_string());
        let notebook_query = TermQuery::new(notebook_term, IndexRecordOption::Basic);

        // Combine: must match notebook AND text query
        let combined_query = BooleanQuery::new(vec![
            (Occur::Must, Box::new(notebook_query)),
            (Occur::Must, text_query),
        ]);

        // Execute search
        let top_docs = searcher
            .search(&combined_query, &TopDocs::with_limit(limit))
            .map_err(|e| SearchError::SearchExecutionError(format!("search failed: {}", e)))?;

        // Create snippet generator for the content field
        let snippet_generator = tantivy::snippet::SnippetGenerator::create(
            &searcher,
            &*self
                .query_parser
                .parse_query(query_str)
                .unwrap_or_else(|_| Box::new(tantivy::query::AllQuery)),
            self.fields.content,
        )
        .map_err(|e| {
            SearchError::SearchExecutionError(format!("snippet generator failed: {}", e))
        })?;

        // Convert results to SearchHit
        let mut hits = Vec::with_capacity(top_docs.len());
        for (score, doc_address) in top_docs {
            let doc: tantivy::TantivyDocument = searcher.doc(doc_address).map_err(|e| {
                SearchError::SearchExecutionError(format!("failed to fetch doc: {}", e))
            })?;

            // Extract entry_id
            let entry_id_str: &str = doc
                .get_first(self.fields.entry_id)
                .and_then(|v| v.as_str())
                .ok_or_else(|| SearchError::SearchExecutionError("missing entry_id".to_string()))?;

            let entry_id: EntryId = entry_id_str.parse().map_err(|e| {
                SearchError::SearchExecutionError(format!("invalid entry_id: {}", e))
            })?;

            // Generate snippet
            let snippet = snippet_generator.snippet_from_doc(&doc);
            let snippet_text = if snippet.is_empty() {
                // Fallback: use first MAX_SNIPPET_LENGTH chars of content
                let content = doc
                    .get_first(self.fields.content)
                    .and_then(|v| v.as_str())
                    .unwrap_or("");
                truncate_to_char_boundary(content, MAX_SNIPPET_LENGTH).to_string()
            } else {
                // Use the snippet, removing HTML highlighting tags
                let raw = snippet.to_html();
                strip_html_tags(&raw)
            };

            hits.push(SearchHit {
                entry_id,
                score,
                snippet: snippet_text,
            });
        }

        Ok(hits)
    }

    /// Deletes an entry from the search index.
    ///
    /// # Arguments
    ///
    /// * `entry_id` - The ID of the entry to delete.
    ///
    /// # Errors
    ///
    /// Returns `SearchError::IndexingError` if the entry cannot be deleted.
    pub fn delete_entry(&self, entry_id: EntryId) -> Result<(), SearchError> {
        let mut writer = self.writer.lock()?;

        let term = Term::from_field_text(self.fields.entry_id, &entry_id.to_string());
        writer.delete_term(term);

        writer
            .commit()
            .map_err(|e| SearchError::IndexingError(format!("failed to commit delete: {}", e)))?;

        Ok(())
    }

    /// Forces a reload of the index reader.
    ///
    /// Normally, the reader auto-reloads after commits. This method can be used
    /// to force an immediate reload if needed.
    pub fn reload(&self) -> Result<(), SearchError> {
        self.reader
            .reload()
            .map_err(|e| SearchError::IndexError(format!("failed to reload reader: {}", e)))
    }
}

/// Truncates a string to a maximum number of characters, respecting UTF-8 boundaries.
fn truncate_to_char_boundary(s: &str, max_chars: usize) -> &str {
    if s.chars().count() <= max_chars {
        s
    } else {
        let mut end = 0;
        for (i, (idx, _)) in s.char_indices().enumerate() {
            if i >= max_chars {
                break;
            }
            end = idx + s[idx..].chars().next().map(|c| c.len_utf8()).unwrap_or(0);
        }
        &s[..end]
    }
}

/// Strips simple HTML tags from a string.
fn strip_html_tags(s: &str) -> String {
    let mut result = String::with_capacity(s.len());
    let mut in_tag = false;

    for c in s.chars() {
        if c == '<' {
            in_tag = true;
        } else if c == '>' {
            in_tag = false;
        } else if !in_tag {
            result.push(c);
        }
    }

    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use notebook_core::types::AuthorId;
    use tempfile::TempDir;

    fn create_test_entry(content: &str, topic: Option<&str>) -> Entry {
        Entry::builder()
            .content(content.as_bytes().to_vec())
            .content_type("text/plain")
            .topic(topic.unwrap_or("test"))
            .author(AuthorId::zero())
            .build()
    }

    #[test]
    fn test_index_and_search() {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();

        let notebook_id = NotebookId::new();
        let entry = create_test_entry(
            "The quick brown fox jumps over the lazy dog",
            Some("animals"),
        );

        index.index_entry(notebook_id, &entry).unwrap();

        // Wait for commit to be visible
        std::thread::sleep(std::time::Duration::from_millis(100));
        index.reload().unwrap();

        let hits = index.search("quick fox", notebook_id, 10).unwrap();
        assert_eq!(hits.len(), 1);
        assert_eq!(hits[0].entry_id, entry.id);
        assert!(hits[0].score > 0.0);
    }

    #[test]
    fn test_notebook_isolation() {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();

        let notebook1 = NotebookId::new();
        let notebook2 = NotebookId::new();

        let entry1 = create_test_entry("Knowledge exchange platform", None);
        let entry2 = create_test_entry("Another knowledge system", None);

        index.index_entry(notebook1, &entry1).unwrap();
        index.index_entry(notebook2, &entry2).unwrap();

        std::thread::sleep(std::time::Duration::from_millis(100));
        index.reload().unwrap();

        // Search in notebook1 should only find entry1
        let hits1 = index.search("knowledge", notebook1, 10).unwrap();
        assert_eq!(hits1.len(), 1);
        assert_eq!(hits1[0].entry_id, entry1.id);

        // Search in notebook2 should only find entry2
        let hits2 = index.search("knowledge", notebook2, 10).unwrap();
        assert_eq!(hits2.len(), 1);
        assert_eq!(hits2[0].entry_id, entry2.id);
    }

    #[test]
    fn test_delete_entry() {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();

        let notebook_id = NotebookId::new();
        let entry = create_test_entry("Unique searchable content", None);
        let entry_id = entry.id;

        index.index_entry(notebook_id, &entry).unwrap();
        std::thread::sleep(std::time::Duration::from_millis(100));
        index.reload().unwrap();

        // Should find the entry
        let hits = index.search("unique", notebook_id, 10).unwrap();
        assert_eq!(hits.len(), 1);

        // Delete the entry
        index.delete_entry(entry_id).unwrap();
        std::thread::sleep(std::time::Duration::from_millis(100));
        index.reload().unwrap();

        // Should not find the entry anymore
        let hits = index.search("unique", notebook_id, 10).unwrap();
        assert_eq!(hits.len(), 0);
    }

    #[test]
    fn test_update_entry() {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();

        let notebook_id = NotebookId::new();
        let entry = create_test_entry("Original content", None);
        let entry_id = entry.id;

        index.index_entry(notebook_id, &entry).unwrap();

        // Update with new content but same ID
        let updated_entry = Entry::builder()
            .id(entry_id)
            .content(b"Updated content with different words".to_vec())
            .content_type("text/plain")
            .author(AuthorId::zero())
            .build();

        index.index_entry(notebook_id, &updated_entry).unwrap();
        std::thread::sleep(std::time::Duration::from_millis(100));
        index.reload().unwrap();

        // Should find with new content
        let hits = index.search("updated different", notebook_id, 10).unwrap();
        assert_eq!(hits.len(), 1);

        // Should not find with old content
        let hits = index.search("original", notebook_id, 10).unwrap();
        assert_eq!(hits.len(), 0);
    }

    #[test]
    fn test_topic_search() {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();

        let notebook_id = NotebookId::new();
        let entry = create_test_entry("Some generic content", Some("architecture design"));

        index.index_entry(notebook_id, &entry).unwrap();
        std::thread::sleep(std::time::Duration::from_millis(100));
        index.reload().unwrap();

        // Should find by topic
        let hits = index.search("architecture", notebook_id, 10).unwrap();
        assert_eq!(hits.len(), 1);
    }

    #[test]
    fn test_truncate_to_char_boundary() {
        assert_eq!(truncate_to_char_boundary("hello", 10), "hello");
        assert_eq!(truncate_to_char_boundary("hello", 3), "hel");
        assert_eq!(truncate_to_char_boundary("", 5), "");

        // Test with multi-byte characters
        let s = "hello \u{1F600} world"; // emoji is 4 bytes
        let truncated = truncate_to_char_boundary(s, 8);
        assert!(truncated.is_char_boundary(truncated.len()));
    }

    #[test]
    fn test_strip_html_tags() {
        assert_eq!(strip_html_tags("<b>bold</b>"), "bold");
        assert_eq!(strip_html_tags("no tags here"), "no tags here");
        assert_eq!(
            strip_html_tags("<em>test</em> more <b>text</b>"),
            "test more text"
        );
    }
}
