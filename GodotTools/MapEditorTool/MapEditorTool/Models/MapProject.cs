using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MapEditorTool.Models
{
    public sealed class MapProject
    {
        public MapProject()
        {
            Maps = new List<MapDefinition>();
            Links = new List<MapLink>();
        }

        public List<MapDefinition> Maps { get; set; }
        public List<MapLink> Links { get; set; }

        public static MapProject CreateDefault()
        {
            var project = new MapProject();
            project.ResetToDefault();
            return project;
        }

        public void ResetToDefault()
        {
            Maps.Clear();
            Links.Clear();

            Maps.Add(new MapDefinition
            {
                Id = "main",
                DisplayName = "Main",
                Kind = MapKind.Vertical,
                RoomWidth = 27,
                RoomHeight = 15
            });
        }

        public void CopyFrom(MapProject other)
        {
            if (other == null)
                return;

            Maps = other.Maps ?? new List<MapDefinition>();
            Links = other.Links ?? new List<MapLink>();
        }

        public void RemoveMapById(string mapId)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            mapId = (mapId ?? string.Empty).Trim();
            if (mapId.Length > 0)
                ids.Add(mapId);

            foreach (var map in Maps)
            {
                if (string.Equals((map.Id ?? string.Empty).Trim(), mapId, StringComparison.Ordinal) ||
                    string.Equals((map.ScenePath ?? string.Empty).Trim(), mapId, StringComparison.Ordinal))
                {
                    AddMapIdentity(ids, map);
                }
            }

            Maps.RemoveAll(m =>
                ids.Contains((m.Id ?? string.Empty).Trim()) ||
                ids.Contains((m.ScenePath ?? string.Empty).Trim()));
            Links.RemoveAll(l =>
                l == null ||
                l.From == null ||
                l.To == null ||
                ids.Contains((l.From.MapId ?? string.Empty).Trim()) ||
                ids.Contains((l.To.MapId ?? string.Empty).Trim()));
        }

        private static void AddMapIdentity(HashSet<string> ids, MapDefinition map)
        {
            if (map == null)
                return;

            var id = (map.Id ?? string.Empty).Trim();
            if (id.Length > 0)
                ids.Add(id);

            var scenePath = (map.ScenePath ?? string.Empty).Trim();
            if (scenePath.Length > 0)
                ids.Add(scenePath);
        }
    }

    public sealed class MapDefinition
    {
        public MapDefinition()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            Kind = MapKind.Vertical;
            RoomWidth = 27;
            RoomHeight = 15;
            ScenePath = string.Empty;
            TemplateTexturePath = string.Empty;
            ForegroundTexturePath = string.Empty;
            ForegroundTextureNodePath = string.Empty;
            BackgroundTexturePath = string.Empty;
            BackgroundNodePath = string.Empty;
            ForegroundTextureUpscale = 1.0f;
            BackgroundTextureUpscale = 1.0f;
            BackgroundTileLayerVisible = true;
            CollisionUsed = CollisionMode.TileForeground;
            TileCollisionDataPath = string.Empty;
            ForegroundTextureCollisionDataPath = string.Empty;
            Entities = new List<PlacedEntity>();
            Obstacles = new List<Obstacle>();
            Skin = new TileSkin();
            Portals = new List<Portal>();
            TileLayers = new List<TileLayer>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public MapKind Kind { get; set; }
        public int RoomWidth { get; set; }
        public int RoomHeight { get; set; }
        public string ScenePath { get; set; }
        public string TemplateTexturePath { get; set; }
        public bool ForegroundTextureEnabled { get; set; }
        public string ForegroundTexturePath { get; set; }
        public TextureAnchor ForegroundTextureAnchor { get; set; }
        public float ForegroundTextureUpscale { get; set; }
        [Browsable(false)]
        public string ForegroundTextureNodePath { get; set; }
        public bool BackgroundTextureEnabled { get; set; }
        public string BackgroundTexturePath { get; set; }
        public TextureAnchor BackgroundTextureAnchor { get; set; }
        public float BackgroundTextureUpscale { get; set; }
        public bool BackgroundTileLayerVisible { get; set; }
        [Browsable(false)]
        public string BackgroundNodePath { get; set; }
        public CollisionMode CollisionUsed { get; set; }
        public string TileCollisionDataPath { get; set; }
        public string ForegroundTextureCollisionDataPath { get; set; }
        public List<PlacedEntity> Entities { get; set; }
        public List<Obstacle> Obstacles { get; set; }
        public TileSkin Skin { get; set; }
        public List<Portal> Portals { get; set; }
        public List<TileLayer> TileLayers { get; set; }
    }

    public enum CollisionMode
    {
        TileForeground = 0,
        ForegroundTexture = 1
    }

    public enum TextureAnchor
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3,
        Center = 4
    }

    public enum MapKind
    {
        Vertical = 0,
        SideScroller = 1,
        Isometric = 2,
        Overworld = 3
    }

    public sealed class PlacedEntity
    {
        public PlacedEntity()
        {
            Id = Guid.NewGuid().ToString("N");
            Type = "Entity";
            Prefab = string.Empty;
            NodePath = string.Empty;
            Pushable = true;
            Props = new Dictionary<string, object>();
        }

        public string Id { get; set; }
        public string Type { get; set; }
        public string Prefab { get; set; }
        public string NodePath { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool Pushable { get; set; }
        public Dictionary<string, object> Props { get; set; }
    }

    public sealed class Obstacle
    {
        public Obstacle()
        {
            Id = Guid.NewGuid().ToString("N");
            Type = "Obstacle";
            Prefab = string.Empty;
            Width = 64;
            Height = 64;
            Static = true;
            Props = new Dictionary<string, object>();
        }

        public string Id { get; set; }
        public string Type { get; set; }
        public string Prefab { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool Static { get; set; }
        public Dictionary<string, object> Props { get; set; }
    }

    public sealed class TileSkin
    {
        public TileSkin()
        {
            TileSetId = "default";
            ForegroundSourceId = 1;
            BackgroundSourceId = 1;
            Props = new Dictionary<string, object>();
        }

        public string TileSetId { get; set; }
        public int ForegroundSourceId { get; set; }
        public int BackgroundSourceId { get; set; }
        public Dictionary<string, object> Props { get; set; }
    }

    public sealed class TileLayer
    {
        public TileLayer()
        {
            Name = string.Empty;
            NodePath = string.Empty;
            TileSetPath = string.Empty;
            Visible = true;
            Cells = new List<TileCell>();
            Props = new Dictionary<string, object>();
        }

        public string Name { get; set; }
        public string NodePath { get; set; }
        public string TileSetPath { get; set; }
        public bool Visible { get; set; }
        public int ZIndex { get; set; }
        [Browsable(false)]
        public List<TileCell> Cells { get; set; }
        public Dictionary<string, object> Props { get; set; }
    }

    public sealed class TileCell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int SourceId { get; set; }
        public int AtlasX { get; set; }
        public int AtlasY { get; set; }
        public int Alternative { get; set; }
    }

    public sealed class Portal
    {
        public Portal()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Portal";
            NodePath = string.Empty;
            TargetMapId = string.Empty;
            TargetPortalId = string.Empty;
            AnimationVideoPath = string.Empty;
            AnimationFps = 12.0f;
            Upscale = 0.25f;
            KeyoutTolerance = 5;
            AnimationFramesDir = string.Empty;
            Props = new Dictionary<string, object>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string NodePath { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string TargetMapId { get; set; }
        public string TargetPortalId { get; set; }
        public string AnimationVideoPath { get; set; }
        public float AnimationFps { get; set; }
        public int AnimationFrameCount { get; set; }
        public float AnimationDurationSec { get; set; }
        public float Upscale { get; set; }
        public int KeyoutTolerance { get; set; }
        public string AnimationFramesDir { get; set; }
        public Dictionary<string, object> Props { get; set; }
    }

    public sealed class MapLink
    {
        public MapLink()
        {
            From = new LinkEndpoint();
            To = new LinkEndpoint();
            Props = new Dictionary<string, object>();
        }

        public LinkEndpoint From { get; set; }
        public LinkEndpoint To { get; set; }
        public Dictionary<string, object> Props { get; set; }

        public string DisplayName
        {
            get { return From.MapId + " -> " + To.MapId; }
        }
    }

    public sealed class LinkEndpoint
    {
        public LinkEndpoint()
        {
            MapId = string.Empty;
            PortalId = string.Empty;
        }

        public string MapId { get; set; }
        public string PortalId { get; set; }
    }
}
