#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ToonBoom.TBGImporter
{
    public class TBGXmlData
    {
        public int exportVersion;
        public IEnumerable<SpriteSheetSettings> spriteSheets;
        public IEnumerable<SkeletonSettings> skeletons;
        public Dictionary<string, DrawingAnimationSettings> drawingAnimations;
        public Dictionary<string, AnimationSettings> animations;
        public ILookup<string, StageSettings> skeletonToStages;
        public TBGXmlData(AssetImportContext ctx)
        {
            XDocument spriteSheetsXML;
            XDocument skeletonXML;
            XDocument drawingAnimationXML;
            XDocument animationXML;
            XDocument stageXML;

#nullable enable
            using (var file = new FileStream(ctx.assetPath, FileMode.Open))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                XElement? spriteSheetsTag = null;
                var spriteSheetsFile = archive.GetEntry("spriteSheets.xml");
                if (spriteSheetsFile != null)
                {
                    using (var spriteSheetReader = new StreamReader(spriteSheetsFile.Open(), Encoding.Default))
                        spriteSheetsXML = XDocument
                            .Parse(spriteSheetReader.ReadToEnd());
                    spriteSheetsTag = spriteSheetsXML
                        .Element("spritesheets");
                }
                spriteSheets = spriteSheetsTag != null
                    ? spriteSheetsTag
                        .Elements("spritesheet")
                        .Select(spriteSheet => new SpriteSheetSettings
                        {
                            name = (string)spriteSheet.Attribute("name"),
                            filename = (string)spriteSheet.Attribute("filename"),
                            resolution = (string)spriteSheet.Attribute("resolution"),
                            width = (int)spriteSheet.Attribute("width"),
                            height = (int)spriteSheet.Attribute("height"),
                            sprites = spriteSheet
                                .Elements("sprite")
                                .Select(sprite => new SpriteSettings
                                {
                                    rect = ((string)sprite.Attribute("rect"))
                                        .Split(',')
                                        .Select(value => int.Parse(value))
                                        .ToArray(),
                                    scaleX = (double)sprite.Attribute("scaleX"),
                                    scaleY = (double)sprite.Attribute("scaleY"),
                                    offsetX = (double)sprite.Attribute("offsetX"),
                                    offsetY = (double)sprite.Attribute("offsetY"),
                                    name = (string)sprite.Attribute("name"),
                                })
                            .OrderBy(sprite => sprite.name),
                        })
                    : archive.Entries
                        .Where(entry => Path.GetExtension(entry.Name) == ".sprite")
                        .Select(entry =>
                        {
                            using var spriteReader = new StreamReader(entry.Open(), Encoding.Default);
                            var crop = XDocument.Parse(spriteReader.ReadToEnd())
                                .Element("crop");
                            var pathSplits = entry.FullName.Split('/');
                            return new
                            {
                                spriteSheetName = pathSplits[1],
                                resolution = pathSplits[2],
                                sprite = new SpriteSettings
                                {
                                    name = Path.GetFileNameWithoutExtension(entry.FullName),
                                    filename = string.Join(".", entry.FullName.Split('.').Reverse().Skip(1).Reverse()),
                                    scaleX = (double)crop.Attribute("scaleX"),
                                    scaleY = (double)crop.Attribute("scaleY"),
                                    offsetX = (double)crop.Attribute("pivotX"),
                                    offsetY = (double)crop.Attribute("pivotY"),
                                }
                            };
                        })
                        .ToLookup(entry => entry.resolution, entry => new { entry.sprite, entry.spriteSheetName })
                        .Select(entry => new SpriteSheetSettings
                        {
                            resolution = entry.Key,
                            name = entry.First().spriteSheetName,
                            sprites = entry.Select(entry => entry.sprite),
                        });
            }

            using (var file = new FileStream(ctx.assetPath, FileMode.Open))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                using (var skeletonReader = new StreamReader(archive.GetEntry("skeleton.xml").Open(), Encoding.Default))
                    skeletonXML = XDocument
                        .Parse(skeletonReader.ReadToEnd());
                using (var drawingAnimationReader = new StreamReader(archive.GetEntry("drawingAnimation.xml").Open(), Encoding.Default))
                    drawingAnimationXML = XDocument
                        .Parse(drawingAnimationReader.ReadToEnd());
                using (var animationReader = new StreamReader(archive.GetEntry("animation.xml").Open(), Encoding.Default))
                    animationXML = XDocument
                        .Parse(animationReader.ReadToEnd());
                using (var stageReader = new StreamReader(archive.GetEntry("stage.xml").Open(), Encoding.Default))
                    stageXML = XDocument
                        .Parse(stageReader.ReadToEnd());
            }

            exportVersion = (int)skeletonXML.Element("skeletons").Attribute("version");

            skeletons = skeletonXML
                .Element("skeletons")
                .Elements("skeleton")
                .Select(skeleton => new SkeletonSettings
                {
                    name = (string)skeleton.Attribute("name"),
                    nodes = skeleton
                        .Element("nodes")
                        .Elements()
                        .Select(element => new NodeSettings
                        {
                            tag = element.Name.ToString(),
                            id = (int)element.Attribute("id"),
                            name = (string)element.Attribute("name"),
                            visible = (bool?)element.Attribute("visible"),
                        }),
                    links = skeleton
                        .Element("links")
                        .Elements("link")
                        .Select(link => new LinkSettings
                        {
                            nodeIn = (string)link.Attribute("in") == "Top" ? -1 : (int)link.Attribute("in"),
                            nodeOut = (int)link.Attribute("out"),
                            port = (int?)link.Attribute("port"),
                        }),
                });

            drawingAnimations = drawingAnimationXML
                .Element("drawingAnimations")
                .Elements("drawingAnimation")
                .Select(drawingAnimation => new DrawingAnimationSettings
                {
                    name = (string)drawingAnimation.Attribute("name"),
                    spritesheet = (string)drawingAnimation.Attribute("spritesheet"),
                    drawings = drawingAnimation
                        .Elements("drawing")
                        .Select(drawing => new DrawingSettings
                        {
                            node = (string)drawing.Attribute("node"),
                            // name = (string)drawing.Attribute("name"), // Same as "node"
                            // drwId = (int)drawing.Attribute("drwId"), // Redundant lookup to "node"
                            drws = drawing
                                .Elements("drw")
                                .Select(drw => new DrwSettings
                                {
                                    skinId = (int?)drw.Attribute("skinId") ?? 0,
                                    name = (string)drw.Attribute("name"),
                                    frame = (int)drw.Attribute("frame"),
                                    repeat = (int?)drw.Attribute("repeat") ?? 1,
                                }),
                        })
                        .ToDictionary(entry => entry.node, entry => entry.drws),
                })
                .ToDictionary(entry => entry.name, entry => entry);

            animations = animationXML
                .Element("animations")
                .Elements("animation")
                .Select(animation => new AnimationSettings
                {
                    name = (string)animation.Attribute("name"),
                    attrlinks = animation
                        .Element("attrlinks")
                        .Elements("attrlink")
                        .Select(attrlink => new AttrLinkSettings
                        {
                            node = (string)attrlink.Attribute("node"),
                            attr = (string)attrlink.Attribute("attr"),
                            timedvalue = (string?)attrlink.Attribute("timedvalue") ?? null,
                            value = (double?)attrlink.Attribute("value") ?? 0,
                        }),
                    timedvalues = animation
                        .Element("timedvalues")
                        .Elements()
                        .Select(timed => new TimedValueSettings
                        {
                            tag = timed.Name.ToString(),
                            name = (string)timed.Attribute("name"),
                            points = timed
                                .Elements("pt")
                                .Select(point => new TimedValuePoint
                                {
                                    x = (double)point.Attribute("x"),
                                    y = (double)point.Attribute("y"),
                                    z = (double?)point.Attribute("z") ?? 0.0f,
                                    lx = (double?)point.Attribute("lx") ?? 0.0f,
                                    ly = (double?)point.Attribute("ly") ?? 0.0f,
                                    rx = (double?)point.Attribute("rx") ?? 0.0f,
                                    ry = (double?)point.Attribute("ry") ?? 0.0f,
                                    lockedInTime = (double?)point.Attribute("lockedInTime"),
                                    constSeg = (bool?)point.Attribute("constSeg") ?? false,
                                    start = (int?)point.Attribute("start"),
                                })
                                .ToArray(),
                        })
                        .ToLookup(entry => entry.name, entry => entry),
                })
                .ToDictionary(animation => animation.name, animation => animation);

            skeletonToStages = stageXML
               .Element("stages")
                .Elements("stage")
                .Select(stage =>
                {
                    var stageSettings = new StageSettings
                    {
                        name = (string)stage.Attribute("name"),
                        skins = new List<SkinSettings>(),
                        groups = new List<GroupSettings>(),
                        metadata = new List<Metadata>(),
                        nodes = stage
                            .Elements("node")
                            .Select(node => new StageNodeSettings
                            {
                                drwId = (int)node.Attribute("drwId"),
                                name = (string)node.Attribute("name"),
                                groupId = (int)node.Attribute("groupId"),
                                skinIds = ((string)node.Attribute("skinId"))
                                    .Split(',')
                                    .Where(value => value.Length > 0)
                                    .Select(value =>  int.Parse(value))
                                    .ToArray(),
                            }),
                        play = stage
                            .Elements("play")
                            .Select(play => new PlaySettings
                            {
                                name = (string)play.Attribute("name"),
                                animation = (string)play.Attribute("animation"),
                                drawingAnimation = (string)play.Attribute("drawingAnimation"),
                                skeleton = (string)play.Attribute("skeleton"),
                                framerate = play.Attributes("framerate")
                                    .Select(element => (int)element)
                                    .DefaultIfEmpty(30)
                                    .First(),
                            })
                            .First(),
                    };
                    foreach (var element in stage.Elements())
                    {
                        switch (element.Name.ToString())
                        {
                            case "skin":
                                stageSettings.skins.Add(new SkinSettings
                                {
                                    skinId = (int)element.Attribute("skinId"),
                                    name = (string)element.Attribute("name"),
                                });
                                break;
                            case "group":
                                stageSettings.groups.Add(new GroupSettings
                                {
                                    groupId = (int)element.Attribute("groupId"),
                                    name = (string)element.Attribute("name"),
                                });
                                break;
                            case "meta":
                                stageSettings.metadata.Add(new Metadata
                                {
                                    node = (string)element.Attribute("node"),
                                    name = (string)element.Attribute("name"),
                                    value = (string)element.Attribute("value"),
                                });
                                break;
                            case "sound":
                                stageSettings.sound = stage
                                    .Elements("sound")
                                    .Select(sound => new SoundSettings
                                    {
                                        name = (string)sound.Attribute("name"),
                                        time = (int)sound.Attribute("time"),
                                    })
                                    .First();
                                break;
                        }
                    }
                    return stageSettings;
                })
                .ToLookup(entry => entry.play.skeleton, entry => entry);
#nullable disable
        }
    }
}

#endif