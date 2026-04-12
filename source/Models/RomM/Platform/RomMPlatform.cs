using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Platform
{
public class RomMPlatform : IEquatable<RomMPlatform>
    {
        public bool Equals(RomMPlatform? other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(other, this)) return true;
            return this.Id == other.Id;
        }

        public sealed override bool Equals(object? obj)
        {
            var otherMyItem = obj as RomMPlatform;
            if (Object.ReferenceEquals(otherMyItem, null)) return false;
            return otherMyItem.Equals(this);
        }

        public static bool operator ==(RomMPlatform? myItem1, RomMPlatform? myItem2)
        {
            return Object.Equals(myItem1, myItem2);
        }

        public static bool operator !=(RomMPlatform? myItem1, RomMPlatform? myItem2)
        {
            return !(myItem1 == myItem2);
        }
        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("fs_slug")]
        public string? FsSlug { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("rom_count")]
        public int RomCount { get; set; }

        [JsonPropertyName("igdb_id")]
        public ulong? IgdbId { get; set; }

        [JsonPropertyName("sgdb_id")]
        public object? SgdbId { get; set; }

        [JsonPropertyName("moby_id")]
        public object? MobyId { get; set; }

        [JsonPropertyName("logo_path")]
        public string? LogoPath { get; set; }

        [JsonPropertyName("firmware")]
        public List<RomMPlatformFirmware>? Firmware { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }


}