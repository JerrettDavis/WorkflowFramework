using System.Collections.Concurrent;
using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// In-memory store for voice transcription submissions.
/// </summary>
public sealed class VoiceSubmissionStore
{
    private readonly ConcurrentDictionary<string, VoiceSubmission> _submissions = new();

    public IReadOnlyList<VoiceSubmission> List(int limit = 50)
    {
        return _submissions.Values
            .OrderByDescending(s => s.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    public VoiceSubmission? Get(string id)
    {
        _submissions.TryGetValue(id, out var submission);
        return submission;
    }

    public VoiceSubmission Save(VoiceSubmissionRequest request)
    {
        var submission = new VoiceSubmission
        {
            WorkflowId = request.WorkflowId,
            WorkflowName = request.WorkflowName,
            Transcript = request.Transcript,
            Language = request.Language,
            AudioFileName = request.AudioFileName,
            AudioMimeType = request.AudioMimeType,
            AudioSizeBytes = request.AudioSizeBytes,
            Parameters = request.Parameters is null ? null : new Dictionary<string, string>(request.Parameters),
            QaPairs = request.QaPairs.Select(q => new VoiceQaPair
            {
                Question = q.Question,
                Answer = q.Answer
            }).ToList()
        };

        _submissions[submission.Id] = submission;
        return submission;
    }
}
