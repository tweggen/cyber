//! TF-IDF (Term Frequency-Inverse Document Frequency) implementation.
//!
//! This module provides text analysis capabilities for the coherence model:
//! - Tokenization with Unicode support
//! - Stop word removal for English text
//! - TF-IDF weight computation
//! - Cosine similarity for document comparison
//!
//! The implementation is intentionally simple, using only basic string operations
//! and hash maps rather than external NLP libraries.

use std::collections::{HashMap, HashSet};
use unicode_segmentation::UnicodeSegmentation;

/// Common English stop words to filter from text analysis.
/// These words occur frequently but carry little semantic meaning.
const STOP_WORDS: &[&str] = &[
    "a",
    "about",
    "above",
    "after",
    "again",
    "against",
    "all",
    "am",
    "an",
    "and",
    "any",
    "are",
    "as",
    "at",
    "be",
    "because",
    "been",
    "before",
    "being",
    "below",
    "between",
    "both",
    "but",
    "by",
    "can",
    "could",
    "did",
    "do",
    "does",
    "doing",
    "down",
    "during",
    "each",
    "few",
    "for",
    "from",
    "further",
    "had",
    "has",
    "have",
    "having",
    "he",
    "her",
    "here",
    "hers",
    "herself",
    "him",
    "himself",
    "his",
    "how",
    "i",
    "if",
    "in",
    "into",
    "is",
    "it",
    "its",
    "itself",
    "just",
    "me",
    "more",
    "most",
    "my",
    "myself",
    "no",
    "nor",
    "not",
    "now",
    "of",
    "off",
    "on",
    "once",
    "only",
    "or",
    "other",
    "our",
    "ours",
    "ourselves",
    "out",
    "over",
    "own",
    "same",
    "she",
    "should",
    "so",
    "some",
    "such",
    "than",
    "that",
    "the",
    "their",
    "theirs",
    "them",
    "themselves",
    "then",
    "there",
    "these",
    "they",
    "this",
    "those",
    "through",
    "to",
    "too",
    "under",
    "until",
    "up",
    "very",
    "was",
    "we",
    "were",
    "what",
    "when",
    "where",
    "which",
    "while",
    "who",
    "whom",
    "why",
    "will",
    "with",
    "would",
    "you",
    "your",
    "yours",
    "yourself",
    "yourselves",
];

/// Minimum token length to consider (shorter tokens are filtered).
const MIN_TOKEN_LENGTH: usize = 2;

/// Tokenizes text into a list of normalized tokens.
///
/// Processing steps:
/// 1. Split on Unicode word boundaries
/// 2. Convert to lowercase
/// 3. Remove punctuation (keep alphanumeric and hyphens)
/// 4. Filter by minimum length
/// 5. Remove stop words
///
/// # Arguments
///
/// * `text` - The input text to tokenize
///
/// # Returns
///
/// A vector of normalized token strings
pub fn tokenize(text: &str) -> Vec<String> {
    let stop_words: HashSet<&str> = STOP_WORDS.iter().copied().collect();

    text.unicode_words()
        .map(|word| normalize_token(word))
        .filter(|token| token.len() >= MIN_TOKEN_LENGTH && !stop_words.contains(token.as_str()))
        .collect()
}

/// Normalizes a single token by lowercasing and removing non-alphanumeric characters.
fn normalize_token(token: &str) -> String {
    token
        .chars()
        .filter(|c| c.is_alphanumeric() || *c == '-')
        .collect::<String>()
        .to_lowercase()
}

/// Computes term frequency for a document.
///
/// Term frequency is calculated as: count(term) / total_terms
///
/// # Arguments
///
/// * `tokens` - The tokenized document
///
/// # Returns
///
/// A map from term to its frequency in the document
pub fn term_frequency(tokens: &[String]) -> HashMap<String, f64> {
    if tokens.is_empty() {
        return HashMap::new();
    }

    let total = tokens.len() as f64;
    let mut counts: HashMap<String, usize> = HashMap::new();

    for token in tokens {
        *counts.entry(token.clone()).or_insert(0) += 1;
    }

    counts
        .into_iter()
        .map(|(term, count)| (term, count as f64 / total))
        .collect()
}

/// Statistics about a corpus of documents for IDF computation.
#[derive(Debug, Clone, Default, serde::Serialize, serde::Deserialize)]
pub struct CorpusStats {
    /// Number of documents in the corpus
    pub document_count: usize,
    /// Number of documents containing each term
    pub document_frequencies: HashMap<String, usize>,
}

impl CorpusStats {
    /// Creates a new empty corpus stats.
    pub fn new() -> Self {
        Self::default()
    }

    /// Adds a document's tokens to the corpus statistics.
    ///
    /// Updates document count and document frequencies for each unique term.
    pub fn add_document(&mut self, tokens: &[String]) {
        self.document_count += 1;

        // Count each unique term once per document
        let unique_terms: HashSet<&String> = tokens.iter().collect();
        for term in unique_terms {
            *self.document_frequencies.entry(term.clone()).or_insert(0) += 1;
        }
    }

    /// Computes the inverse document frequency for a term.
    ///
    /// IDF = log(N / df) where N is total documents and df is document frequency.
    /// Returns 0.0 if the term is not in the corpus or corpus is empty.
    pub fn idf(&self, term: &str) -> f64 {
        if self.document_count == 0 {
            return 0.0;
        }

        match self.document_frequencies.get(term) {
            Some(&df) if df > 0 => (self.document_count as f64 / df as f64).ln(),
            _ => 0.0,
        }
    }
}

/// A TF-IDF weighted document vector.
#[derive(Debug, Clone, Default, serde::Serialize, serde::Deserialize)]
pub struct TfIdfVector {
    /// Map from term to TF-IDF weight
    pub weights: HashMap<String, f64>,
}

impl TfIdfVector {
    /// Creates a TF-IDF vector from document tokens and corpus statistics.
    pub fn from_tokens(tokens: &[String], corpus: &CorpusStats) -> Self {
        let tf = term_frequency(tokens);
        let weights = tf
            .into_iter()
            .map(|(term, freq)| {
                let idf = corpus.idf(&term);
                (term, freq * idf)
            })
            .filter(|(_, weight)| *weight > 0.0)
            .collect();

        Self { weights }
    }

    /// Computes the L2 norm (magnitude) of the vector.
    pub fn magnitude(&self) -> f64 {
        self.weights.values().map(|w| w * w).sum::<f64>().sqrt()
    }

    /// Computes the dot product with another TF-IDF vector.
    pub fn dot(&self, other: &TfIdfVector) -> f64 {
        self.weights
            .iter()
            .filter_map(|(term, weight)| {
                other
                    .weights
                    .get(term)
                    .map(|other_weight| weight * other_weight)
            })
            .sum()
    }

    /// Computes cosine similarity with another TF-IDF vector.
    ///
    /// Returns 0.0 if either vector has zero magnitude.
    pub fn cosine_similarity(&self, other: &TfIdfVector) -> f64 {
        let mag_self = self.magnitude();
        let mag_other = other.magnitude();

        if mag_self == 0.0 || mag_other == 0.0 {
            return 0.0;
        }

        self.dot(other) / (mag_self * mag_other)
    }

    /// Returns the top N terms by TF-IDF weight.
    pub fn top_terms(&self, n: usize) -> Vec<String> {
        let mut terms: Vec<_> = self.weights.iter().collect();
        terms.sort_by(|a, b| b.1.partial_cmp(a.1).unwrap_or(std::cmp::Ordering::Equal));
        terms
            .into_iter()
            .take(n)
            .map(|(term, _)| term.clone())
            .collect()
    }

    /// Checks if the vector is empty (no terms with positive weight).
    pub fn is_empty(&self) -> bool {
        self.weights.is_empty()
    }
}

/// Merges multiple TF-IDF vectors by summing their weights.
///
/// Useful for computing cluster-level keyword importance.
pub fn merge_vectors(vectors: &[&TfIdfVector]) -> TfIdfVector {
    let mut merged = HashMap::new();

    for vector in vectors {
        for (term, weight) in &vector.weights {
            *merged.entry(term.clone()).or_insert(0.0) += weight;
        }
    }

    TfIdfVector { weights: merged }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tokenize_basic() {
        let tokens = tokenize("Hello, world! This is a test.");
        // "a" and "is" are stop words, "this" is a stop word
        assert!(tokens.contains(&"hello".to_string()));
        assert!(tokens.contains(&"world".to_string()));
        assert!(tokens.contains(&"test".to_string()));
        assert!(!tokens.contains(&"a".to_string()));
        assert!(!tokens.contains(&"is".to_string()));
    }

    #[test]
    fn tokenize_empty() {
        let tokens = tokenize("");
        assert!(tokens.is_empty());
    }

    #[test]
    fn tokenize_only_punctuation() {
        let tokens = tokenize("... ??? !!!");
        assert!(tokens.is_empty());
    }

    #[test]
    fn tokenize_unicode() {
        let tokens = tokenize("cafe resume naive");
        assert_eq!(tokens.len(), 3);
    }

    #[test]
    fn tokenize_short_words_filtered() {
        let tokens = tokenize("I am a big cat");
        // "I", "am", "a" are stop words or too short
        assert!(tokens.contains(&"big".to_string()));
        assert!(tokens.contains(&"cat".to_string()));
        assert!(!tokens.contains(&"i".to_string()));
    }

    #[test]
    fn term_frequency_basic() {
        let tokens = vec!["cat".into(), "dog".into(), "cat".into(), "bird".into()];
        let tf = term_frequency(&tokens);

        assert!((tf["cat"] - 0.5).abs() < 0.001);
        assert!((tf["dog"] - 0.25).abs() < 0.001);
        assert!((tf["bird"] - 0.25).abs() < 0.001);
    }

    #[test]
    fn term_frequency_empty() {
        let tokens: Vec<String> = vec![];
        let tf = term_frequency(&tokens);
        assert!(tf.is_empty());
    }

    #[test]
    fn corpus_stats_add_document() {
        let mut corpus = CorpusStats::new();
        corpus.add_document(&["cat".into(), "dog".into(), "cat".into()]);
        corpus.add_document(&["cat".into(), "bird".into()]);

        assert_eq!(corpus.document_count, 2);
        assert_eq!(corpus.document_frequencies["cat"], 2);
        assert_eq!(corpus.document_frequencies["dog"], 1);
        assert_eq!(corpus.document_frequencies["bird"], 1);
    }

    #[test]
    fn idf_computation() {
        let mut corpus = CorpusStats::new();
        corpus.add_document(&["cat".into(), "dog".into()]);
        corpus.add_document(&["cat".into(), "bird".into()]);
        corpus.add_document(&["fish".into(), "bird".into()]);

        // cat appears in 2 of 3 docs: ln(3/2) ~ 0.405
        let idf_cat = corpus.idf("cat");
        assert!((idf_cat - 0.405).abs() < 0.01);

        // bird appears in 2 of 3 docs: ln(3/2) ~ 0.405
        let idf_bird = corpus.idf("bird");
        assert!((idf_bird - 0.405).abs() < 0.01);

        // dog appears in 1 of 3 docs: ln(3/1) ~ 1.099
        let idf_dog = corpus.idf("dog");
        assert!((idf_dog - 1.099).abs() < 0.01);

        // unknown term
        assert_eq!(corpus.idf("unknown"), 0.0);
    }

    #[test]
    fn tfidf_vector_creation() {
        let mut corpus = CorpusStats::new();
        corpus.add_document(&["cat".into(), "dog".into()]);
        corpus.add_document(&["cat".into(), "bird".into()]);
        corpus.add_document(&["fish".into(), "bird".into()]);

        let tokens = vec!["cat".into(), "cat".into(), "dog".into()];
        let vector = TfIdfVector::from_tokens(&tokens, &corpus);

        // "cat" appears in 2/3 docs → IDF > 0; "dog" in 1/3 → IDF > 0
        assert!(vector.weights.contains_key("cat"));
        assert!(vector.weights.contains_key("dog"));
    }

    #[test]
    fn cosine_similarity_identical() {
        let mut corpus = CorpusStats::new();
        corpus.add_document(&["cat".into(), "dog".into()]);
        corpus.add_document(&["bird".into(), "fish".into()]);

        let tokens = vec!["cat".into(), "dog".into()];
        let v1 = TfIdfVector::from_tokens(&tokens, &corpus);
        let v2 = TfIdfVector::from_tokens(&tokens, &corpus);

        let sim = v1.cosine_similarity(&v2);
        assert!((sim - 1.0).abs() < 0.001);
    }

    #[test]
    fn cosine_similarity_orthogonal() {
        let mut corpus = CorpusStats::new();
        corpus.add_document(&["cat".into()]);
        corpus.add_document(&["dog".into()]);

        let v1 = TfIdfVector::from_tokens(&["cat".into()], &corpus);
        let v2 = TfIdfVector::from_tokens(&["dog".into()], &corpus);

        let sim = v1.cosine_similarity(&v2);
        assert!(sim.abs() < 0.001);
    }

    #[test]
    fn cosine_similarity_empty() {
        let corpus = CorpusStats::new();
        let v1 = TfIdfVector::from_tokens(&[], &corpus);
        let v2 = TfIdfVector::from_tokens(&[], &corpus);

        let sim = v1.cosine_similarity(&v2);
        assert_eq!(sim, 0.0);
    }

    #[test]
    fn top_terms() {
        let mut weights = HashMap::new();
        weights.insert("high".into(), 0.9);
        weights.insert("medium".into(), 0.5);
        weights.insert("low".into(), 0.1);

        let vector = TfIdfVector { weights };
        let top = vector.top_terms(2);

        assert_eq!(top.len(), 2);
        assert_eq!(top[0], "high");
        assert_eq!(top[1], "medium");
    }

    #[test]
    fn merge_vectors_basic() {
        let mut w1 = HashMap::new();
        w1.insert("cat".into(), 0.5);
        w1.insert("dog".into(), 0.3);
        let v1 = TfIdfVector { weights: w1 };

        let mut w2 = HashMap::new();
        w2.insert("cat".into(), 0.2);
        w2.insert("bird".into(), 0.4);
        let v2 = TfIdfVector { weights: w2 };

        let merged = merge_vectors(&[&v1, &v2]);

        assert!((merged.weights["cat"] - 0.7).abs() < 0.001);
        assert!((merged.weights["dog"] - 0.3).abs() < 0.001);
        assert!((merged.weights["bird"] - 0.4).abs() < 0.001);
    }

    #[test]
    fn corpus_stats_serialization() {
        let mut corpus = CorpusStats::new();
        corpus.add_document(&["cat".into(), "dog".into()]);

        let json = serde_json::to_string(&corpus).unwrap();
        let parsed: CorpusStats = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.document_count, corpus.document_count);
        assert_eq!(parsed.document_frequencies, corpus.document_frequencies);
    }

    #[test]
    fn tfidf_vector_serialization() {
        let mut weights = HashMap::new();
        weights.insert("test".into(), 0.42);
        let vector = TfIdfVector { weights };

        let json = serde_json::to_string(&vector).unwrap();
        let parsed: TfIdfVector = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.weights, vector.weights);
    }
}
