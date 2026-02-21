using Microsoft.Extensions.Logging;
using Notebook.Server.Services.Crawlers;

namespace Notebook.Tests.Services.Crawlers;

public class ConfluenceCrawlerTests
{
    [Fact]
    public void NotebookBatchEntry_WithMetadata_IsValid()
    {
        // Arrange
        var entry = new NotebookBatchEntry
        {
            Content = "# Test\n\nContent",
            ContentType = "text/markdown",
            SourceHint = "confluence",
            Metadata = new Dictionary<string, object>
            {
                ["source_url"] = "https://confluence.example.com/pages/123",
                ["confluence_page_id"] = "123",
                ["confluence_version"] = 5
            }
        };

        // Assert
        Assert.NotNull(entry.Content);
        Assert.Equal("text/markdown", entry.ContentType);
        Assert.Equal("confluence", entry.SourceHint);
        Assert.Contains("source_url", entry.Metadata.Keys);
    }

    [Fact]
    public void ConfluenceSpace_WithData_IsValid()
    {
        // Arrange
        var space = new ConfluenceSpace
        {
            Id = 12345,
            Key = "ENG",
            Name = "Engineering"
        };

        // Assert
        Assert.Equal(12345, space.Id);
        Assert.Equal("ENG", space.Key);
        Assert.Equal("Engineering", space.Name);
    }

    [Fact]
    public void ConfluencePage_WithLabels_IsValid()
    {
        // Arrange
        var page = new ConfluencePage
        {
            Id = "123",
            Title = "API Documentation",
            Status = "current",
            HtmlBody = "<p>Content</p>",
            Version = new ConfluenceVersion { Number = 5, CreatedAt = DateTime.UtcNow },
            WebUrl = "https://confluence.example.com/pages/123",
            Labels = new List<string> { "published", "current" }
        };

        // Assert
        Assert.Equal("123", page.Id);
        Assert.Equal("API Documentation", page.Title);
        Assert.Contains("published", page.Labels);
        Assert.Equal(2, page.Labels.Count);
    }
}

public class ConfluenceConfigValidatorTests
{
    private readonly CrawlerConfigValidator _validator;

    public ConfluenceConfigValidatorTests()
    {
        // Create a null logger for testing
        var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<CrawlerConfigValidator>();
        _validator = new CrawlerConfigValidator(logger);
    }

    [Fact]
    public void ValidateConfluenceConfig_WithValidJson_Succeeds()
    {
        // Arrange
        var configJson = """
            {
              "base_url": "https://company.atlassian.net/wiki",
              "username": "user@company.com",
              "api_token": "ATATT3xFfGF0...",
              "space_key": "ENG",
              "max_pages": 1000
            }
            """;

        // Act & Assert - should not throw
        _validator.ValidateConfluenceConfig(configJson);
    }

    [Fact]
    public void ValidateConfluenceConfig_WithMissingRequiredField_Throws()
    {
        // Arrange
        var configJson = """
            {
              "base_url": "https://company.atlassian.net/wiki",
              "username": "user@company.com",
              "api_token": "ATATT3xFfGF0..."
            }
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _validator.ValidateConfluenceConfig(configJson));
        Assert.Contains("space_key", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceConfig_WithInvalidSpaceKey_Throws()
    {
        // Arrange
        var configJson = """
            {
              "base_url": "https://company.atlassian.net/wiki",
              "username": "user@company.com",
              "api_token": "ATATT3xFfGF0...",
              "space_key": "invalid-lowercase"
            }
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _validator.ValidateConfluenceConfig(configJson));
        Assert.Contains("space_key", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceConfig_WithInvalidJson_Throws()
    {
        // Arrange
        var configJson = "{ invalid json }";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _validator.ValidateConfluenceConfig(configJson));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void ConfluenceConfig_FromJson_ParsesCorrectly()
    {
        // Arrange
        var configJson = """
            {
              "base_url": "https://company.atlassian.net/wiki",
              "username": "user@company.com",
              "api_token": "ATATT3xFfGF0...",
              "space_key": "ENG",
              "include_labels": ["published"],
              "exclude_labels": ["draft"],
              "max_pages": 1000,
              "include_attachments": true
            }
            """;

        var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<CrawlerConfigValidator>();
        var validator = new CrawlerConfigValidator(logger);

        // Act
        var config = ConfluenceConfig.FromJson(configJson, validator);

        // Assert
        Assert.Equal("https://company.atlassian.net/wiki", config.BaseUrl);
        Assert.Equal("user@company.com", config.Username);
        Assert.Equal("ATATT3xFfGF0...", config.ApiToken);
        Assert.Equal("ENG", config.SpaceKey);
        Assert.Single(config.IncludeLabels);
        Assert.Equal("published", config.IncludeLabels[0]);
        Assert.Single(config.ExcludeLabels);
        Assert.Equal("draft", config.ExcludeLabels[0]);
        Assert.Equal(1000, config.MaxPages);
        Assert.True(config.IncludeAttachments);
    }

    [Fact]
    public void ConfluenceConfig_FromJson_WithDefaults_ParsesCorrectly()
    {
        // Arrange
        var configJson = """
            {
              "base_url": "https://company.atlassian.net/wiki",
              "username": "user@company.com",
              "api_token": "ATATT3xFfGF0...",
              "space_key": "DOCS"
            }
            """;

        var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<CrawlerConfigValidator>();
        var validator = new CrawlerConfigValidator(logger);

        // Act
        var config = ConfluenceConfig.FromJson(configJson, validator);

        // Assert
        Assert.Equal("DOCS", config.SpaceKey);
        Assert.Empty(config.IncludeLabels);
        Assert.Empty(config.ExcludeLabels);
        Assert.Equal(0, config.MaxPages); // 0 = unlimited
        Assert.False(config.IncludeAttachments);
    }

    [Fact]
    public void ConfluenceSyncState_ToJson_AndBack_PreservesData()
    {
        // Arrange
        var state = new ConfluenceSyncState
        {
            SpaceKey = "ENG",
            SpaceId = 12345,
            LastSyncTimestamp = DateTime.UtcNow,
            PagesSynced = 42,
            PageMetadata = new Dictionary<string, ConfluencePageMetadata>
            {
                ["123"] = new()
                {
                    Title = "API Docs",
                    Version = 5,
                    LastModified = DateTime.UtcNow.AddMinutes(-10),
                    Status = "current",
                    ContentHash = "abc123"
                }
            }
        };

        var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<CrawlerConfigValidator>();
        var validator = new CrawlerConfigValidator(logger);

        // Act
        var json = state.ToJson();
        var restored = ConfluenceSyncState.FromJson(json, validator);

        // Assert
        Assert.Equal("ENG", restored.SpaceKey);
        Assert.Equal(12345, restored.SpaceId);
        Assert.Equal(42, restored.PagesSynced);
        Assert.Single(restored.PageMetadata);
        Assert.True(restored.PageMetadata.ContainsKey("123"));
        Assert.Equal("API Docs", restored.PageMetadata["123"].Title);
        Assert.Equal(5, restored.PageMetadata["123"].Version);
    }
}
