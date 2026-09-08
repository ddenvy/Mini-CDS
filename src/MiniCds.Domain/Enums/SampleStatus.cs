namespace MiniCds.Domain.Enums;

/// <summary>
/// Lifecycle stages of a chromatographic sample.
/// </summary>

public enum SampleStatus
{
    /// <summary>Sample is queued, acquistion not started.</summary>
    Queued = 0,
    /// <summary>Sample is running, acquistion in progress.</summary>
    Running = 1,
    /// <summary>Sample is completed, acquistion finished.</summary>
    Completed = 2,  
    /// <summary>Sample is voided,Physical sample not available.</summary>
    Voided = 3,
}
