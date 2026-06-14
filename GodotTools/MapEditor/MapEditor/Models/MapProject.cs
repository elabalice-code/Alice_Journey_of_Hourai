using System.ComponentModel;
using System.Drawing.Design;

namespace MapEditor.Models;

public sealed class MapProject
{
    [DisplayName("地图列表")]
    [Description("工程里包含的所有房间/地图。通常对应 Godot 的 CoreEngine/Maps/*.tscn。")]
    public List<MapDefinition> Maps { get; set; } = [];

    [DisplayName("地图连接")]
    [Description("地图与地图之间的出入口连接（通常由 Portal 的 target_map 推导出来，也可以手工编辑）。")]
    public List<MapLink> Links { get; set; } = [];

    public static MapProject CreateDefault()
    {
        var p = new MapProject();
        p.ResetToDefault();
        return p;
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
        Maps = other.Maps ?? [];
        Links = other.Links ?? [];
    }

    public void RemoveMapById(string mapId)
    {
        Maps.RemoveAll(m => m.Id == mapId);
        Links.RemoveAll(l => l.From.MapId == mapId || l.To.MapId == mapId);
    }
}

public sealed class MapDefinition
{
    [DisplayName("ID")]
    [Description("地图唯一标识。导入自 Godot 时默认为该场景的 res:// 路径。")]
    public string Id { get; set; } = "";

    [DisplayName("显示名称")]
    [Description("用于列表显示的名称。默认来自场景文件名（不含 .tscn）。")]
    public string DisplayName { get; set; } = "";

    [DisplayName("地图类型")]
    [Description("地图类型/风格。纵版、横板、等距、俯视等，用于后续编辑器的布局与边框规则。")]
    public MapKind Kind { get; set; } = MapKind.Vertical;

    [DisplayName("房间宽度(格)")]
    [Description("房间网格宽度（Tile 维度）。默认 27。")]
    public int RoomWidth { get; set; } = 27;

    [DisplayName("房间高度(格)")]
    [Description("房间网格高度（Tile 维度）。默认 15。")]
    public int RoomHeight { get; set; } = 15;

    [DisplayName("场景路径")]
    [Description("Godot 场景路径（res://...）。用于定位该地图对应的 .tscn。")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string ScenePath { get; set; } = "";

    [DisplayName("模板纹理")]
    [Description("TemplateRoomMap.gd 的 template 纹理来源（如果该房间使用模板图生成地形）。")]
    [Category("贴图")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string TemplateTexturePath { get; set; } = "";

    [DisplayName("启用前景贴图")]
    [Description("开启后：使用“前景纹理”作为 Tile 背后的前景贴图（层级：前景Tile＞背景Tile＞前景纹理＞背景纹理）。")]
    [Category("贴图")]
    public bool ForegroundTextureEnabled { get; set; } = false;

    [DisplayName("前景纹理")]
    [Description("前景纹理（建议带 Alpha 通道；透明区域表示可通行空间）。普通房间写回 ForegroundTextureLayer/ForegroundTexture；模板房间写回 TemplateRoomMap.gd 的 foreground_texture。")]
    [Category("贴图")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string ForegroundTexturePath { get; set; } = "";

    [DisplayName("前景纹理锚点")]
    [Description("前景纹理在房间矩形内的锚点位置（不做自动拉伸/裁剪）。")]
    [Category("贴图")]
    public TextureAnchor ForegroundTextureAnchor { get; set; } = TextureAnchor.TopLeft;

    [DisplayName("前景纹理放大倍数")]
    [Description("前景纹理的等比放大倍数（不做自动适配；允许溢出房间范围）。")]
    [Category("贴图")]
    public float ForegroundTextureUpscale { get; set; } = 1.0f;

    [Browsable(false)]
    public string ForegroundTextureNodePath { get; set; } = "";

    [DisplayName("启用背景贴图")]
    [Description("开启后：优先使用“背景纹理”显示全屏背景（模板房间写回 background_texture；普通房间写回 BackgroundLayer/BackgroundTexture）。")]
    [Category("贴图")]
    public bool BackgroundTextureEnabled { get; set; } = false;

    [DisplayName("背景纹理")]
    [Description("TemplateRoomMap.gd 的 background_texture 纹理来源；或 BackgroundTexture(TextureRect)/texture。用于定位你替换过的背景图（例如星空）。")]
    [Category("贴图")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string BackgroundTexturePath { get; set; } = "";

    [DisplayName("背景纹理锚点")]
    [Description("背景纹理在房间矩形内的锚点位置（不做自动拉伸/裁剪）。")]
    [Category("贴图")]
    public TextureAnchor BackgroundTextureAnchor { get; set; } = TextureAnchor.TopLeft;

    [DisplayName("背景纹理放大倍数")]
    [Description("背景纹理的等比放大倍数（不做自动适配；允许溢出房间范围）。")]
    [Category("贴图")]
    public float BackgroundTextureUpscale { get; set; } = 1.0f;

    [DisplayName("显示背景图层")]
    [Description("开启后：显示 TileMap 的背景图层（Background/Bakcground/Backgroud/Backgroud）。")]
    [Category("贴图")]
    public bool BackgroundTileLayerVisible { get; set; } = true;

    [Browsable(false)]
    public string BackgroundNodePath { get; set; } = "";

    [DisplayName("碰撞模式")]
    [Description("选择游戏引擎实际使用哪套碰撞数据（两套都会保存到各自文件）。")]
    [Category("碰撞")]
    public CollisionMode CollisionUsed { get; set; } = CollisionMode.TileForeground;

    [DisplayName("Tile碰撞文件")]
    [Description("基于 Tile 前景生成/编辑的碰撞数据文件（res://...）。")]
    [Category("碰撞")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string TileCollisionDataPath { get; set; } = "";

    [DisplayName("前景纹理碰撞文件")]
    [Description("基于前景纹理（Alpha）生成/编辑的碰撞数据文件（res://...）。")]
    [Category("碰撞")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string ForegroundTextureCollisionDataPath { get; set; } = "";

    [DisplayName("物件/人物")]
    [Description("可摆放的实体（人物、道具、可推动物体等）。导入时会根据实例化的 PackedScene 粗略分类。")]
    public List<PlacedEntity> Entities { get; set; } = [];

    [DisplayName("固定障碍")]
    [Description("固定地形/障碍物配置（本项目目前未从 .tscn 自动提取，后续可扩展）。")]
    public List<Obstacle> Obstacles { get; set; } = [];

    [DisplayName("地形皮肤")]
    [Description("TileSet 与 source_id 等皮肤信息。")]
    [Category("皮肤")]
    public TileSkin Skin { get; set; } = new();

    [DisplayName("出入口(Portal)")]
    [Description("房间内的出入口。导入时会读取 Portal/实例节点的 target_map。")]
    [Editor(typeof(MapEditor.MainForm.PortalCollectionEditor), typeof(UITypeEditor))]
    public List<Portal> Portals { get; set; } = [];

    [DisplayName("TileMap 图层")]
    [Description("房间内的 TileMapLayer 图层与其 TileSet 绑定信息。")]
    public List<TileLayer> TileLayers { get; set; } = [];
}

public enum CollisionMode
{
    [Description("基于 Tile 前景生成/使用碰撞。")]
    TileForeground = 0,
    [Description("基于前景纹理（Alpha）生成/使用碰撞。")]
    ForegroundTexture = 1
}

public enum TextureAnchor
{
    [Description("左上")]
    TopLeft = 0,
    [Description("右上")]
    TopRight = 1,
    [Description("左下")]
    BottomLeft = 2,
    [Description("右下")]
    BottomRight = 3,
    [Description("居中")]
    Center = 4
}

public enum MapKind
{
    [Description("纵版房间：偏竖向布局（本项目现用）。")]
    Vertical = 0,
    [Description("横板房间：偏横向布局。")]
    SideScroller = 1,
    [Description("等距视角：类似以撒等透视/梯形边框。")]
    Isometric = 2,
    [Description("俯视/大地图：更宽广的平面地图。")]
    Overworld = 3
}

public sealed class PlacedEntity
{
    [DisplayName("ID")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [DisplayName("类型")]
    public string Type { get; set; } = "Entity";
    [DisplayName("预制体")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string Prefab { get; set; } = "";

    [DisplayName("节点路径")]
    [Description("在 .tscn 里的节点路径（用于写回 position）。")]
    public string NodePath { get; set; } = "";

    [DisplayName("X")]
    public float X { get; set; }
    [DisplayName("Y")]
    public float Y { get; set; }

    [DisplayName("可推动")]
    public bool Pushable { get; set; } = true;
    [DisplayName("扩展属性")]
    public Dictionary<string, object?> Props { get; set; } = [];
}

public sealed class Obstacle
{
    [DisplayName("ID")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [DisplayName("类型")]
    public string Type { get; set; } = "Obstacle";
    [DisplayName("预制体")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string Prefab { get; set; } = "";

    [DisplayName("X")]
    public float X { get; set; }
    [DisplayName("Y")]
    public float Y { get; set; }
    [DisplayName("宽度")]
    public float Width { get; set; } = 64;
    [DisplayName("高度")]
    public float Height { get; set; } = 64;

    [DisplayName("静态")]
    public bool Static { get; set; } = true;
    [DisplayName("扩展属性")]
    public Dictionary<string, object?> Props { get; set; } = [];
}

public sealed class TileSkin
{
    [DisplayName("TileSet")]
    [Category("皮肤")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string TileSetId { get; set; } = "default";
    [DisplayName("前景 source_id")]
    [Category("皮肤")]
    public int ForegroundSourceId { get; set; } = 1;
    [DisplayName("背景 source_id")]
    [Category("皮肤")]
    public int BackgroundSourceId { get; set; } = 1;
    [DisplayName("扩展属性")]
    [Category("皮肤")]
    public Dictionary<string, object?> Props { get; set; } = [];
}

public sealed class TileLayer
{
    [DisplayName("名称")]
    public string Name { get; set; } = "";
    [DisplayName("节点路径")]
    public string NodePath { get; set; } = "";
    [DisplayName("TileSet 路径")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string TileSetPath { get; set; } = "";
    [DisplayName("可见")]
    [Description("对应 TileMapLayer 的 visible 属性（缺省为 true）。")]
    public bool Visible { get; set; } = true;
    [DisplayName("Z序")]
    [Description("对应 TileMapLayer 的 z_index（缺省为 0）。")]
    public int ZIndex { get; set; } = 0;
    [Browsable(false)]
    public List<TileCell> Cells { get; set; } = [];
    [DisplayName("扩展属性")]
    public Dictionary<string, object?> Props { get; set; } = [];
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
    [DisplayName("ID")]
    [ReadOnly(true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [DisplayName("名称")]
    public string Name { get; set; } = "Portal";

    [DisplayName("节点路径")]
    [Description("在 .tscn 里的节点路径（用于写回 position/target_map）。")]
    [ReadOnly(true)]
    public string NodePath { get; set; } = "";

    [DisplayName("X")]
    public float X { get; set; }
    [DisplayName("Y")]
    public float Y { get; set; }
    [DisplayName("目标地图")]
    [TypeConverter(typeof(MapEditor.MainForm.PortalTargetMapIdConverter))]
    [Editor(typeof(MapEditor.MainForm.PortalTargetMapIdEditor), typeof(UITypeEditor))]
    public string TargetMapId { get; set; } = "";
    [DisplayName("目标入口ID")]
    [TypeConverter(typeof(MapEditor.MainForm.PortalTargetPortalIdConverter))]
    [Editor(typeof(MapEditor.MainForm.PortalTargetPortalIdEditor), typeof(UITypeEditor))]
    public string TargetPortalId { get; set; } = "";

    [DisplayName("动画视频")]
    [Description("传送门触发动画的视频文件（*.mp4）。选择后可预览，并自动拆帧生成门动画资源目录。")]
    [Editor(typeof(MapEditor.GodotResPathEditor), typeof(UITypeEditor))]
    public string AnimationVideoPath { get; set; } = "";

    [DisplayName("动画帧率")]
    [Description("触发动画的播放帧率（fps）。")]
    public float AnimationFps { get; set; } = 12.0f;

    [DisplayName("目标帧数")]
    [Description("从视频中均匀抽取的帧数（>0）。为 0 则导入全部帧。")]
    public int AnimationFrameCount { get; set; } = 0;

    [DisplayName("播放时长(秒)")]
    [Description("在指定时长内播放完整动画（>0）。例如 2 秒播放 12 帧。为 0 则使用“动画帧率”。")]
    public float AnimationDurationSec { get; set; } = 0.0f;

    [DisplayName("Upscale")]
    [Description("传送门显示放大倍数（>0）。会写回 portal_upscale。")]
    public float Upscale { get; set; } = 0.25f;

    [DisplayName("黑底容差")]
    [Description("拆帧后将黑色背景抠成透明的容差（0-255）。参考 Photoshop 的“容差”概念。")]
    public int KeyoutTolerance { get; set; } = 5;

    [DisplayName("动画资源目录")]
    [Description("由动画视频拆帧生成的目录（res://...），会写回 portal_anim_dir。")]
    [ReadOnly(true)]
    public string AnimationFramesDir { get; set; } = "";
    [DisplayName("扩展属性")]
    public Dictionary<string, object?> Props { get; set; } = [];
}

public sealed class MapLink
{
    [DisplayName("起点")]
    public LinkEndpoint From { get; set; } = new();
    [DisplayName("终点")]
    public LinkEndpoint To { get; set; } = new();
    [DisplayName("扩展属性")]
    public Dictionary<string, object?> Props { get; set; } = [];

    public string DisplayName => $"{From.MapId} -> {To.MapId}";
}

public sealed class LinkEndpoint
{
    [DisplayName("地图ID")]
    public string MapId { get; set; } = "";
    [DisplayName("入口ID")]
    public string PortalId { get; set; } = "";
}
