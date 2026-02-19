namespace WorkflowFramework.Samples.VoiceWorkflows;

/// <summary>LLM prompt templates for voice workflows.</summary>
public static class PromptTemplates
{
    public const string CleanupPrompt =
        """
        Clean up the following raw transcript. Fix grammar, remove filler words (um, uh, like),
        and improve readability while preserving the original meaning and tone.
        Do not add or remove content — only polish what's there.

        Transcript:
        {transcript}
        """;

    public const string MeetingNotesPrompt =
        """
        Convert the following speaker-labeled transcript into structured meeting notes.
        Use this format:
        ## Meeting Notes
        ### Attendees
        ### Key Discussion Points
        ### Decisions Made
        ### Action Items (with owners if identifiable)

        Transcript:
        {transcript}
        """;

    public const string ActionItemsPrompt =
        """
        Extract all action items from the following meeting notes.
        Return them as a JSON array: [{"owner": "...", "task": "...", "deadline": "..."}]
        If owner or deadline is unknown, use null.

        Meeting Notes:
        {notes}
        """;

    public const string BlogPostPrompt =
        """
        Synthesize the following Q&A pairs into a polished blog post.
        Write in a conversational but professional tone. Include an introduction,
        organize the content thematically (not just Q&A order), and add a conclusion.

        Q&A Content:
        {qa_content}
        """;

    public const string SynthesisPrompt =
        """
        Take the following brain dump transcript and organize it into a structured document.
        Group related ideas into sections with clear headings.
        Preserve ALL ideas — do not discard anything, even if it seems tangential.
        Add brief transitions between sections.

        Brain Dump:
        {transcript}
        """;

    public const string QuestionGenerationPrompt =
        """
        Based on the following topic introduction, generate 5 thoughtful interview questions.
        The questions should be open-ended, explore different angles, and build on each other.
        Use the available tools to analyze the topic if helpful.
        Return the questions as a numbered list.

        Topic Introduction:
        {topic}
        """;

    public const string SummarizePrompt =
        """
        Create an executive summary of the following transcript.
        Keep it to 3-5 paragraphs covering the main themes, key insights, and notable quotes.

        Transcript:
        {transcript}
        """;
}
