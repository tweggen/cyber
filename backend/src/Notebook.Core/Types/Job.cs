namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>Type of work for robot workers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobType>))]
public enum JobType
{
    [JsonStringEnumMemberName("DISTILL_CLAIMS")]
    DistillClaims,

    [JsonStringEnumMemberName("COMPARE_CLAIMS")]
    CompareClaims,

    [JsonStringEnumMemberName("CLASSIFY_TOPIC")]
    ClassifyTopic,
}

/// <summary>Status of a job in the queue.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("in_progress")]
    InProgress,

    [JsonStringEnumMemberName("completed")]
    Completed,

    [JsonStringEnumMemberName("failed")]
    Failed,
}
