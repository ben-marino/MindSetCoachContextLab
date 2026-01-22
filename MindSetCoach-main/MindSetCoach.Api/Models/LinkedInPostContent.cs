namespace MindSetCoach.Api.Models;

/// <summary>
/// Generated LinkedIn post content from experiment insights.
/// </summary>
public class LinkedInPostContent
{
    /// <summary>
    /// First line attention grabber - the hook that stops the scroll.
    /// </summary>
    public string Hook { get; set; } = string.Empty;

    /// <summary>
    /// Main content body with insights and takeaways.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Call to action to drive engagement.
    /// </summary>
    public string CallToAction { get; set; } = string.Empty;

    /// <summary>
    /// Relevant hashtags for discoverability.
    /// </summary>
    public List<string> Hashtags { get; set; } = new();

    /// <summary>
    /// Combined ready-to-copy text with all components formatted.
    /// </summary>
    public string FullPost { get; set; } = string.Empty;

    /// <summary>
    /// Batch ID the insights were generated from.
    /// </summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>
    /// Tone used for generation.
    /// </summary>
    public string Tone { get; set; } = string.Empty;

    /// <summary>
    /// Character count of the full post.
    /// </summary>
    public int CharacterCount => FullPost.Length;

    /// <summary>
    /// Whether the post is within LinkedIn's character limit.
    /// </summary>
    public bool IsWithinLimit => CharacterCount <= 3000;
}
