using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.Models.Firmware;

public class GithubReleaseResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    
    [JsonPropertyName("assets_url")]
    public string? AssetsUrl { get; set; }
    
    [JsonPropertyName("upload_url")]
    public string? UploadUrl { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
    
    [JsonPropertyName("id")]
    public long? Id { get; set; }
    
    [JsonPropertyName("author")]
    public GithubUser? Author { get; set; }
    
    [JsonPropertyName("node_id")]
    public string? NodeId { get; set; }

    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("target_commitish")]
    public string? TargetCommitish { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("draft")]
    public bool? Draft { get; set; }
    
    [JsonPropertyName("immutable")]
    public bool? Immutable { get; set; }
    
    [JsonPropertyName("prerelease")]
    public bool? Prerelease { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
    
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GithubReleaseAsset>? Assets { get; set; } = new();
    
    [JsonPropertyName("tarball_url")]
    public string? TarballUrl { get; set; }

    [JsonPropertyName("zipball_url")]
    public string? ZipballUrl { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("mentions_count")]
    public int? MentionsCount { get; set; }
}