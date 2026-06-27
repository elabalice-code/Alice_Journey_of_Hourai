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

        [DisplayName("Maps")]
        [Description("All imported or authored rooms/maps. Most entries correspond to a Godot .tscn scene.")]
        public List<MapDefinition> Maps { get; set; }

        [DisplayName("Links")]
        [Description("Portal-to-map links. Usually imported from portal target_map data, but they can also be edited manually.")]
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

        [DisplayName("ID")]
        [Description("Stable map identifier. Imported Godot maps usually use the scene res:// path.")]
        public string Id { get; set; }

        [DisplayName("Display Name")]
        [Description("Human-readable name shown in the map list.")]
        public string DisplayName { get; set; }

        [DisplayName("Map Kind")]
        [Description("Editor classification for layout rules and future room-shape behavior.")]
        public MapKind Kind { get; set; }

        [DisplayName("Room Width")]
        [Description("Room width in tile cells.")]
        public int RoomWidth { get; set; }

        [DisplayName("Room Height")]
        [Description("Room height in tile cells.")]
        public int RoomHeight { get; set; }

        [DisplayName("Scene Path")]
        [Description("Godot scene path for this map, usually res://CoreEngine/Maps/...")]
        public string ScenePath { get; set; }

        [Category("Textures")]
        [DisplayName("Template Texture")]
        [Description("TemplateRoomMap texture source, when the room is driven by a template image.")]
        public string TemplateTexturePath { get; set; }

        [Category("Textures")]
        [DisplayName("Enable Foreground Texture")]
        [Description("Uses a foreground image behind foreground/background tiles. The image must include an alpha channel.")]
        public bool ForegroundTextureEnabled { get; set; }

        [Category("Textures")]
        [DisplayName("Foreground Texture")]
        [Description("Foreground image path. Transparent pixels represent traversable space for alpha-based collision generation.")]
        public string ForegroundTexturePath { get; set; }

        [Category("Textures")]
        [DisplayName("Foreground Anchor")]
        [Description("Anchor position used to place the foreground texture inside the room rectangle.")]
        public TextureAnchor ForegroundTextureAnchor { get; set; }

        [Category("Textures")]
        [DisplayName("Foreground Upscale")]
        [Description("Uniform scale applied to the foreground texture. Values below or equal to zero are clamped when written.")]
        public float ForegroundTextureUpscale { get; set; }

        [Browsable(false)]
        public string ForegroundTextureNodePath { get; set; }

        [Category("Textures")]
        [DisplayName("Enable Background Texture")]
        [Description("Uses a full-screen background texture. Enabling this will disable the background tile layer.")]
        public bool BackgroundTextureEnabled { get; set; }

        [Category("Textures")]
        [DisplayName("Background Texture")]
        [Description("Background image path, either template metadata or BackgroundLayer/BackgroundTexture texture.")]
        public string BackgroundTexturePath { get; set; }

        [Category("Textures")]
        [DisplayName("Background Anchor")]
        [Description("Anchor position used to place the background texture inside the room rectangle.")]
        public TextureAnchor BackgroundTextureAnchor { get; set; }

        [Category("Textures")]
        [DisplayName("Background Upscale")]
        [Description("Uniform scale applied to the background texture. Values below or equal to zero are clamped when written.")]
        public float BackgroundTextureUpscale { get; set; }

        [Category("Textures")]
        [DisplayName("Show Background Tile Layer")]
        [Description("Shows the imported TileMap background layer. Enabling this will disable the background texture.")]
        public bool BackgroundTileLayerVisible { get; set; }

        [Browsable(false)]
        public string BackgroundNodePath { get; set; }

        [Category("Collision")]
        [DisplayName("Collision Mode")]
        [Description("Selects which saved collision layout the game should use.")]
        public CollisionMode CollisionUsed { get; set; }

        [Category("Collision")]
        [DisplayName("Tile Collision File")]
        [Description("Collision JSON generated or edited from tile foreground data.")]
        public string TileCollisionDataPath { get; set; }

        [Category("Collision")]
        [DisplayName("Foreground Texture Collision File")]
        [Description("Collision JSON generated or edited from foreground texture alpha data.")]
        public string ForegroundTextureCollisionDataPath { get; set; }

        [DisplayName("Entities")]
        [Description("Imported or authored scene entities that can be displayed and moved in the map preview.")]
        public List<PlacedEntity> Entities { get; set; }

        [DisplayName("Obstacles")]
        [Description("Fixed obstacle records reserved for authored obstacle data.")]
        public List<Obstacle> Obstacles { get; set; }

        [Category("Skin")]
        [DisplayName("Tile Skin")]
        [Description("TileSet and source_id skin information.")]
        public TileSkin Skin { get; set; }

        [DisplayName("Portals")]
        [Description("Portal entries imported from scene nodes. Target fields can be written back to Godot.")]
        public List<Portal> Portals { get; set; }

        [DisplayName("Tile Layers")]
        [Description("Imported TileMapLayer records and their TileSet bindings.")]
        public List<TileLayer> TileLayers { get; set; }
    }

    public enum CollisionMode
    {
        [Description("Use tile foreground collision.")]
        TileForeground = 0,

        [Description("Use foreground texture alpha collision.")]
        ForegroundTexture = 1
    }

    public enum TextureAnchor
    {
        [Description("Top left")]
        TopLeft = 0,

        [Description("Top right")]
        TopRight = 1,

        [Description("Bottom left")]
        BottomLeft = 2,

        [Description("Bottom right")]
        BottomRight = 3,

        [Description("Center")]
        Center = 4
    }

    public enum MapKind
    {
        [Description("Vertical room layout.")]
        Vertical = 0,

        [Description("Side-scroller room layout.")]
        SideScroller = 1,

        [Description("Isometric room layout.")]
        Isometric = 2,

        [Description("Top-down overworld layout.")]
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

        [DisplayName("ID")]
        public string Id { get; set; }

        [DisplayName("Type")]
        public string Type { get; set; }

        [DisplayName("Prefab")]
        public string Prefab { get; set; }

        [DisplayName("Node Path")]
        [Description("Node path inside the .tscn file, used for writing position changes.")]
        public string NodePath { get; set; }

        [DisplayName("X")]
        public float X { get; set; }

        [DisplayName("Y")]
        public float Y { get; set; }

        [DisplayName("Pushable")]
        public bool Pushable { get; set; }

        [DisplayName("Properties")]
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

        [DisplayName("ID")]
        public string Id { get; set; }

        [DisplayName("Type")]
        public string Type { get; set; }

        [DisplayName("Prefab")]
        public string Prefab { get; set; }

        [DisplayName("X")]
        public float X { get; set; }

        [DisplayName("Y")]
        public float Y { get; set; }

        [DisplayName("Width")]
        public float Width { get; set; }

        [DisplayName("Height")]
        public float Height { get; set; }

        [DisplayName("Static")]
        public bool Static { get; set; }

        [DisplayName("Properties")]
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

        [Category("Skin")]
        [DisplayName("TileSet")]
        public string TileSetId { get; set; }

        [Category("Skin")]
        [DisplayName("Foreground source_id")]
        public int ForegroundSourceId { get; set; }

        [Category("Skin")]
        [DisplayName("Background source_id")]
        public int BackgroundSourceId { get; set; }

        [Category("Skin")]
        [DisplayName("Properties")]
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

        [DisplayName("Name")]
        public string Name { get; set; }

        [DisplayName("Node Path")]
        public string NodePath { get; set; }

        [DisplayName("TileSet Path")]
        public string TileSetPath { get; set; }

        [DisplayName("Visible")]
        [Description("Mirrors the TileMapLayer visible property.")]
        public bool Visible { get; set; }

        [DisplayName("Z Index")]
        [Description("Mirrors the TileMapLayer z_index property.")]
        public int ZIndex { get; set; }

        [Browsable(false)]
        public List<TileCell> Cells { get; set; }

        [DisplayName("Properties")]
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

        [DisplayName("ID")]
        public string Id { get; set; }

        [DisplayName("Name")]
        public string Name { get; set; }

        [DisplayName("Node Path")]
        [Description("Node path inside the .tscn file, used for writing position and target changes.")]
        public string NodePath { get; set; }

        [DisplayName("X")]
        public float X { get; set; }

        [DisplayName("Y")]
        public float Y { get; set; }

        [DisplayName("Target Map")]
        public string TargetMapId { get; set; }

        [DisplayName("Target Portal ID")]
        public string TargetPortalId { get; set; }

        [Category("Animation")]
        [DisplayName("Animation Video")]
        [Description("Source video used to generate portal trigger animation frames.")]
        public string AnimationVideoPath { get; set; }

        [Category("Animation")]
        [DisplayName("Animation FPS")]
        public float AnimationFps { get; set; }

        [Category("Animation")]
        [DisplayName("Frame Count")]
        public int AnimationFrameCount { get; set; }

        [Category("Animation")]
        [DisplayName("Duration Seconds")]
        public float AnimationDurationSec { get; set; }

        [Category("Animation")]
        [DisplayName("Upscale")]
        [Description("Portal display scale. Written back as portal_upscale.")]
        public float Upscale { get; set; }

        [Category("Animation")]
        [DisplayName("Keyout Tolerance")]
        [Description("Tolerance used when keying black background pixels to transparent alpha.")]
        public int KeyoutTolerance { get; set; }

        [Category("Animation")]
        [DisplayName("Animation Frames Directory")]
        public string AnimationFramesDir { get; set; }

        [DisplayName("Properties")]
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

        [DisplayName("From")]
        public LinkEndpoint From { get; set; }

        [DisplayName("To")]
        public LinkEndpoint To { get; set; }

        [DisplayName("Properties")]
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

        [DisplayName("Map ID")]
        public string MapId { get; set; }

        [DisplayName("Portal ID")]
        public string PortalId { get; set; }
    }
}
