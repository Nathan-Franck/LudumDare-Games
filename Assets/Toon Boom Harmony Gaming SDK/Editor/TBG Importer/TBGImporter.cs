#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using System.IO.Compression;
using Unity.Collections;
using System.Linq;
using System.Collections.Generic;
using System;
using ToonBoom.TBGRenderer;
using UnityEditor.Animations;
using UnityEngine.U2D;
using UnityEditor.U2D;
using UnityEngine.Profiling;

namespace ToonBoom.TBGImporter
{
    [ScriptedImporter(6, "tbg")]
    public partial class TBGImporter : ScriptedImporter
    {
        [HideInInspector]
        public bool Initialized;
        [Header("Animation")]
        [Tooltip("The number of columns / rows that sprites will get split into if they are being deformed by bones.")]
        [Range(1, 50)]
        public int DiscretizationStep = 6;
        [Tooltip("The number of frames per-second that the animation runs at.")]
        public float Framerate = 0;
        [Tooltip("Disable interpolation between authored frames.")]
        public bool Stepped = true;
        [Tooltip("If no AnimatorController is referenced below, a new AnimatorController asset will be created in the project beside the .tbg file.")]
        public bool CreateAnimatorController = true;
        [Tooltip("Provides a workspace to transition animations to one another given certain conditions. Can be referenced in gameplay scripts to trigger animations during gameplay.")]
        public AnimatorController AnimatorController;
        [Tooltip("Curves duplicated from clip sub-assets inside .tbg file (and referenced in the AnimatorController) will have their curves overridden with new data from the updated .tbg file.")]
        public bool MaintainCurvesOnClonedClips = false;

        [Header("Material")]
        [Tooltip("Shader used to render all sprites in the prefab.")]
        public Shader Shader;
        [Tooltip("Map gamma color space from textures into Linear color space rendering.")]
        public bool SRGBTexture = true;
        [Tooltip("How neighboring pixels of the texture interpolate.")]
        public FilterMode FilterMode = FilterMode.Bilinear;
        [Tooltip("Generate lower resolution textures to render at further distances.")]
        public bool MipmapEnabled = true;
        [Tooltip("If no SpriteAtlas is referenced below, a new SpriteAtlas asset will be created in the project beside the .tbg file.")]
        public bool CreateSpriteAtlas = true;
        [Tooltip("All sprites from the .tbg file can be added to a SpriteAtlas, when done will be able to combine multiple novel SpriteRenderers into a single drawcall, improving rendering performance.")]
        public SpriteAtlas SpriteAtlas;
        public static string[][] DrawingChannelMapElements = new string[][]{
            new string[] {
                "localPosition",
                "localScale",
            },
            new string[] { "x", "y", "z" },
        };
        public static HashSet<string> OffsetAttributes = new HashSet<string>{
            "offset",
            "offset.x",
            "offset.y",
            "pivot",
            "rotation.anglez",
        };
        public static HashSet<string> ReadAttrsRequiringPivotTransform = new HashSet<string> {
            "pivot",
            "scale",
            "scale.x",
            "scale.y",
            "position",
            "position.x",
            "position.y",
            "offset",
            "offset.x",
            "offset.y",
            "rotation.anglez",
        };
        public static string[] DrawingChannelPropertyNames = DrawingChannelMapElements[0]
            .SelectMany(first => DrawingChannelMapElements[1]
            .Select(second => $"{first}.{second}"))
            .ToArray();

        private TextureGenerationOutput ImportTexture(ZipArchiveEntry entry)
        {
            var ms = new MemoryStream();
            entry.Open().CopyTo(ms);
            var loadedPNG = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            loadedPNG.LoadImage(ms.ToArray(), false);
            var pixelColors = loadedPNG.GetPixels32();
            var settings = new TextureGenerationSettings(TextureImporterType.Sprite)
            {
                platformSettings = new TextureImporterPlatformSettings
                {
                    format = TextureImporterFormat.ARGB32,
                    maxTextureSize = 8192,
                },
                sourceTextureInformation = new SourceTextureInformation
                {
                    width = loadedPNG.width,
                    height = loadedPNG.height,
                    containsAlpha = true,
                },
                textureImporterSettings = new TextureImporterSettings
                {
                    textureType = TextureImporterType.Sprite,
                    textureShape = TextureImporterShape.Texture2D,
                    alphaIsTransparency = true,
                    sRGBTexture = SRGBTexture, // Matters between data store and color store
                    mipmapEnabled = MipmapEnabled,
                    alphaSource = TextureImporterAlphaSource.FromInput,
                    filterMode = FilterMode,
                },
            };
            var result = TextureGenerator.GenerateTexture(settings, new NativeArray<Color32>(pixelColors, Allocator.Temp));
            result.texture.name = Path.GetFileNameWithoutExtension(entry.Name);
            return result;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            GameObject prefab = null;
            try
            {
                if (!Initialized)
                {
                    var userSettings = TBGPackageWindow.GetUserSettings();
                    Shader = userSettings.Shader;
                    CreateSpriteAtlas = userSettings.CreateSpriteAtlas;
                    SpriteAtlas = userSettings.SpriteAtlas;
                    Initialized = true;
                }

                var data = new TBGXmlData(ctx);

                if (Framerate == 0)
                {
                    Framerate = data.skeletonToStages.First().First().play.framerate;
                }

                // For now just import first skeleton TODO support multiple skeletons
                var skeleton = data.skeletons.First();
                var stages = data.skeletonToStages[skeleton.name];

                var spriteSheetToSprites = data.spriteSheets
                    .Select(spriteSheet =>
                    {
                        Dictionary<string, Sprite> sprites;
                        if (spriteSheet.filename != null)
                        {
                            TextureGenerationOutput textureGenerationOutput;
                            using (var file = new FileStream(ctx.assetPath, FileMode.Open))
                            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                            {
                                textureGenerationOutput = ImportTexture(archive.GetEntry(spriteSheet.filename));
                            }
                            sprites = spriteSheet.sprites
                                .Select(spriteSettings =>
                                {
                                    var offset = new Vector2((float)spriteSettings.offsetX, (float)spriteSettings.offsetY);
                                    var scale = new Vector2((float)spriteSettings.scaleX, (float)spriteSettings.scaleY);
                                    var rect = new Rect(
                                        x: spriteSettings.rect[0],
                                        y: spriteSheet.height - spriteSettings.rect[1] - spriteSettings.rect[3],
                                        width: Math.Min(spriteSettings.rect[2], textureGenerationOutput.texture.width - spriteSettings.rect[0]),
                                        height: Math.Min(spriteSettings.rect[3], textureGenerationOutput.texture.height - spriteSettings.rect[1]));
                                    var sprite = Sprite.Create(textureGenerationOutput.texture,
                                        rect,
                                        pivot: -offset / new Vector2(rect.width, rect.height) + new Vector2(0.5f, 0.5f),
                                        pixelsPerUnit: 1.0f / (float)spriteSettings.scaleX,
                                        extrude: 0,
                                        meshType: SpriteMeshType.FullRect,
                                        border: Vector4.zero,
                                        generateFallbackPhysicsShape: false);
                                    sprite.name = $"{spriteSheet.resolution}-{spriteSettings.name}";
                                    ctx.AddObjectToAsset(sprite.name, sprite);
                                    return new { spriteSettings.name, sprite };
                                })
                                    .ToDictionary(entry => entry.name, entry => entry.sprite);
                            textureGenerationOutput.texture.hideFlags = HideFlags.HideInHierarchy;
                            ctx.AddObjectToAsset(
                                textureGenerationOutput.texture.name,
                                textureGenerationOutput.texture,
                                textureGenerationOutput.thumbNail);
                        }
                        else
                        {
                            using var file = new FileStream(ctx.assetPath, FileMode.Open);
                            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
                            sprites = spriteSheet.sprites
                                .Select(spriteSettings =>
                                {
                                    var offset = new Vector2((float)spriteSettings.offsetX, (float)spriteSettings.offsetY);
                                    var scale = new Vector2((float)spriteSettings.scaleX, (float)spriteSettings.scaleY);
                                    var textureGenerationOutput = ImportTexture(archive.GetEntry(spriteSettings.filename));
                                    var dimensions = new Vector2(
                                            textureGenerationOutput.texture.width,
                                            textureGenerationOutput.texture.height);
                                    var sprite = Sprite.Create(textureGenerationOutput.texture,
                                        rect: new Rect(0, 0, dimensions.x, dimensions.y),
                                        pivot: offset / dimensions + new Vector2(0.5f, 0.5f),
                                        pixelsPerUnit: 1.0f / (float)spriteSettings.scaleX,
                                        extrude: 0,
                                        meshType: SpriteMeshType.FullRect,
                                        border: Vector4.zero,
                                        generateFallbackPhysicsShape: false);
                                    sprite.name = $"{spriteSheet.resolution}-{spriteSettings.name}";
                                    textureGenerationOutput.texture.name = sprite.name;
                                    textureGenerationOutput.texture.hideFlags = HideFlags.HideInHierarchy;
                                    ctx.AddObjectToAsset(
                                        textureGenerationOutput.texture.name,
                                        textureGenerationOutput.texture,
                                        textureGenerationOutput.thumbNail);
                                    ctx.AddObjectToAsset(sprite.name, sprite);
                                    return new { spriteSettings.name, sprite };
                                })
                                .ToDictionary(entry => entry.name, entry => entry.sprite);
                        }
                        return new { spriteSheet.resolution, sprites };
                    })
                    .ToLookup(entry => entry.resolution, entry => entry.sprites);

                // Create material - TODO share material between all assets
                Material material;
                {
                    if (Shader == null)
                    {
                        Shader = Shader.Find("Harmony/TBGSprite");
                    }
                    material = new Material(Shader)
                    {
                        name = $"{assetName} Material",
                    };
                    ctx.AddObjectToAsset("Material", material);
                }

                prefab = new GameObject("Main Object");
                var animator = prefab.AddComponent<Animator>();
                TBGRenderer.TBGRenderer tbgRenderer;

                // Custom process for synthesizing nodes / links for drawings that have rotation/scale directly on the node.
                {
                    var lastNodeID = skeleton.nodes.Max(node => node.id);
                    var changesToApply = skeleton.nodes
                        .Where(node => node.tag == "read")
                        .GroupBy(node => node.name)
                        .Select(nodeGroup =>
                        {
                            var node = nodeGroup.First();
                            var nodeAttrs = data.animations.First().Value.attrlinks.Where(attrlink => attrlink.node == node.name);
                            var nodeRequiresPivotTransform = nodeAttrs
                                .Where(attrlink => ReadAttrsRequiringPivotTransform.Contains(attrlink.attr))
                                .Any();
                            var pivotID = ++lastNodeID;
                            return !nodeRequiresPivotTransform
                                ? null
                                : new { pivotID, pivotName = $"{node.name}_Pivot", nodeID = node.id, nodeName = node.name };
                        })
                        .Where(change => change != null)
                        .ToList();
                    var nodeIDToChange = changesToApply
                        .ToDictionary(change => change.nodeID, change => change);
                    var nodeNameToChange = changesToApply
                        .ToDictionary(change => change.nodeName, change => change);

                    // MUTATE nodes and links of skeleton with synthetic pivot nodes.
                    skeleton.nodes = skeleton.nodes.Concat(changesToApply.Select(change => new NodeSettings { tag = "peg", id = change.pivotID, name = change.pivotName }));
                    skeleton.links = skeleton.links
                        .SelectMany(link =>
                            {
                                if (!nodeIDToChange.TryGetValue(link.nodeOut, out var change))
                                {
                                    return new[] { link };
                                }
                                return new LinkSettings[] {
                                    new LinkSettings { nodeIn = link.nodeIn, nodeOut = change.pivotID },
                                    new LinkSettings { nodeIn = change.pivotID, nodeOut = link.nodeOut },
                                };
                            })
                        .ToList();

                    // MUTATE animations for rotation and scale of affected drawings to be applied to synthetic pivot nodes.
                    data.animations = data.animations
                        .Select(animationEntry =>
                        {
                            var animation = animationEntry.Value;
                            animation.attrlinks = animation.attrlinks
                                .Select(attrlink =>
                                {
                                    if (!nodeNameToChange.TryGetValue(attrlink.node, out var change))
                                    {
                                        return attrlink;
                                    }
                                    return new AttrLinkSettings
                                    {
                                        attr = attrlink.attr,
                                        node = change.pivotName,
                                        timedvalue = attrlink.timedvalue,
                                        value = attrlink.value,
                                    };
                                });
                            return new { Key = animationEntry.Key, Value = animation };
                        })
                        .ToDictionary(entry => entry.Key, entry => entry.Value);
                }

                // Custom animation channels created from transforms for spriteRenderers to pick sprites
                // TODO: opt out of channels or make them a bit more specialized so that characters with similar rigs
                //      but different drawings can still use the same animations
                Dictionary<int, string> groupIDToName;
                Dictionary<int, string> skinIDToName;
                Dictionary<int, IEnumerable<int>> groupIDToSkinID;
                Dictionary<string, (int groupID, int[] skinIDs)> nodeToSkinGroup;
                {
                    var nodeToGroupID = stages.First().nodes
                        .ToDictionary(node => node.name, node => node.groupId);
                    var nodeToSkinIDs = data.drawingAnimations
                        .SelectMany(drawingAnimation => drawingAnimation.Value.drawings
                            .SelectMany(drawing => drawing.Value
                                .Select(drw => new { node = drawing.Key, drw.skinId })));
                    nodeToSkinGroup = nodeToSkinIDs
                        .ToLookup(entry => entry.node, entry => entry.skinId)
                        .ToDictionary(
                            entry => entry.Key,
                            entry =>
                            {
                                var skinIDs = entry.Distinct().ToArray();
                                return (nodeToGroupID[entry.Key], skinIDs);
                            });
                    groupIDToSkinID = stages.First().nodes
                        .GroupBy(node => node.groupId)
                        .ToDictionary(group => group.Key, group => group.SelectMany(node => node.skinIds).Distinct());
                    groupIDToName = stages.First().groups.ToDictionary(group => group.groupId, group => group.name);
                    skinIDToName = stages.First().skins.ToDictionary(skin => skin.skinId, skin => skin.name);
                }

                Dictionary<string, Dictionary<int, SpriteRenderer>> nodeToSkinToSpriteRenderer = new Dictionary<string, Dictionary<int, SpriteRenderer>>();
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var firstAnimation = data.animations.First().Value;
                var instantiatedNodes = skeleton.nodes
                    .SelectMany(node =>
                    {
                        switch (node.tag)
                        {
                            default:
                            case "deformRoot":
                            case "bone":
                            case "kinematic":
                            case "cutter":
                            case "peg":
                                return new InstantiatedNode[]{ new InstantiatedNode
                                {
                                    id = node.id,
                                    name = node.name,
                                    gameObject = CreateGameObject(
                                        node,
                                        usedNames,
                                        parent: prefab.transform)
                                }};
                            case "read":
                                nodeToSkinToSpriteRenderer[node.name] = new Dictionary<int, SpriteRenderer>();
                                var skinGroup = nodeToSkinGroup.TryGetValue(node.name, out var skinGroupResult)
                                    ? skinGroupResult
                                    : (0, skinIDs: new int[0]);
                                var skinIDs = skinGroup.skinIDs;
                                if (skinIDs.Length == 0)
                                {
                                    skinIDs = new int[] { -1 };
                                }
                                return skinIDs
                                    .Select(skinID =>
                                    {
                                        var gameObject = CreateGameObject(
                                            node,
                                            usedNames,
                                            parent: prefab.transform);
                                        try
                                        {
                                            var spriteName = data.drawingAnimations.First().Value
                                                .drawings[node.name].FirstOrDefault()
                                                .name;
                                            var spriteSheet = spriteSheetToSprites.First();
                                            var sprite = spriteName == null ? null
                                                : !spriteSheet.First().TryGetValue(spriteName, out var spriteResult) ? null
                                                : spriteResult;
                                            var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                                            spriteRenderer.material = material;
                                            spriteRenderer.sprite = sprite;
                                            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
                                            spriteRenderer.enabled = node.visible.HasValue ? node.visible.Value : true;
                                            if (skinID > 1) // Hide all but the first skin.
                                            {
                                                spriteRenderer.enabled = false;
                                            }
                                            nodeToSkinToSpriteRenderer[node.name][skinID] = spriteRenderer;
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.Log($"Could not find matching sprite for {node.name}");
                                            Debug.LogException(e);
                                        }
                                        return new InstantiatedNode { id = node.id, name = node.name, gameObject = gameObject };
                                    });
                        }
                    })
                    .Where(entry => entry != null)
                    .ToList();

                GameObject rootNode = instantiatedNodes.First().gameObject;
                rootNode.transform.parent = prefab.transform;

                Dictionary<string, TransformRestInfo> nodeToRestInfo = new Dictionary<string, TransformRestInfo>();
                Dictionary<string, BoneRestInfo> nodeToBoneRestInfo;
                Dictionary<string, NodeInstance> finalBoneEnds;
                IEnumerable<InstantiatedBone> instantiatedBones;
                {
                    var nodeToPivot = data.animations.Values
                        .SelectMany(animation => animation.attrlinks
                            .Where(attrlink => attrlink.attr == "pivot")
                            .Select(attrlink =>
                            {
                                var point = animation.timedvalues[attrlink.timedvalue].First().points.First();
                                return new { attrlink.node, point = new Vector3((float)point.x, (float)point.y, (float)point.z) };
                            }))
                        .ToLookup(entry => entry.node, entry => entry.point);
                    // Generate bone information
                    var boneGenerator = new TBGBoneGenerator
                    {
                        DiscretizationStep = DiscretizationStep,
                        data = data,
                        spriteSheetToSprites = spriteSheetToSprites,
                        instantiatedNodes = instantiatedNodes,
                    };
                    boneGenerator.GenerateRestInfo(
                        out var nodeToBoneRestOffsetInfo,
                        out nodeToBoneRestInfo,
                        out instantiatedBones,
                        out finalBoneEnds);
                    var idToInstantiatedNodes = instantiatedNodes
                        .ToLookup(entry => entry.id, entry => entry);
                    foreach (var link in skeleton.links)
                    {
                        foreach (var nodeOutInfo in idToInstantiatedNodes[link.nodeOut])
                        {
                            var nodeInInfos = idToInstantiatedNodes[link.nodeIn];
                            if (nodeInInfos.Count() == 0)
                            {
                                if (link.nodeIn >= 0)
                                {
                                    continue;
                                }
                                nodeInInfos = new InstantiatedNode[] { new InstantiatedNode
                                {
                                    id = -1,
                                    name = "root",
                                    gameObject = rootNode
                                }};
                            }
                            foreach (var nodeInInfo in nodeInInfos)
                            {
                                nodeOutInfo.gameObject.transform.parent = nodeInInfo.gameObject.transform;

                                var pivotIn = nodeToPivot[nodeInInfo.name].FirstOrDefault();
                                var pivotOut = nodeToPivot[nodeOutInfo.name].FirstOrDefault();
                                var boneRestInfo = nodeToBoneRestOffsetInfo[nodeOutInfo.name].FirstOrDefault();
                                var pivotOffset = Quaternion.AngleAxis(boneRestInfo.restRootAngle * Mathf.Rad2Deg, Vector3.forward) * (pivotOut - pivotIn);
                                boneRestInfo.restRootPosition += new Vector2(pivotOffset.x, pivotOffset.y);
                                nodeOutInfo.gameObject.transform.localPosition = boneRestInfo.restRootPosition;
                                nodeOutInfo.gameObject.transform.localRotation = Quaternion.AngleAxis(boneRestInfo.restRootAngle * Mathf.Rad2Deg, Vector3.forward);
                                nodeToRestInfo[nodeOutInfo.name] = boneRestInfo;
                            }
                        }
                    }
                    ctx.AddObjectToAsset("Main Object", prefab);
                    ctx.SetMainObject(prefab);
                }

                var nodeToInstantiated = instantiatedNodes.ToLookup(entry => entry.name, entry => entry);

                // Cutter processing on existing instantiated renderers
                TBGStore tbgStore;
                {
                    // Disable cutters' spriterenders, and add entry into TBGRenderer component on base
                    var outToIn = skeleton.links.ToLookup(entry => entry.nodeOut, entry => new { nodePath = entry.nodeIn, port = entry.port });
                    var idToNode = skeleton.nodes.ToDictionary(entry => entry.id, entry => entry);
                    var idToInstantiatedNode = instantiatedNodes
                        .ToLookup(entry => entry.id, entry => entry);
                    var readToSpriteRenderers = stages.First().nodes
                        .Select(node => new TBGRenderer.TBGRenderer.SpriteRenderersEntry
                        {
                            spriteRenderers = nodeToInstantiated[node.name]
                                .Select(instantiated => instantiated.gameObject.GetComponentInChildren<SpriteRenderer>())
                                .ToArray()
                        })
                        .ToArray();
                    var rendererToReadIndex = readToSpriteRenderers
                        .SelectMany((entry, readIndex) => entry.spriteRenderers
                            .Select(renderer => new { renderer, index = (ushort)readIndex }))
                        .ToLookup(entry => entry.renderer, entry => entry.index);
                    var cutters = skeleton.nodes.Where(IsCutter);
                    var cutterEntries = cutters
                        .SelectMany(cutter =>
                        {
                            var inverse = cutter.tag == "inverseCutter";
                            var inputReadNodes = outToIn[cutter.id]
                                .Select(entry => new { port = entry.port, node = idToNode[entry.nodePath] })
                                .Where(entry => entry.node.tag == "read");
                            NodeSettings matteRead;
                            List<NodeSettings> cutteeReads;
                            if (inputReadNodes.Count() == 0)
                            {
                                return new CutterEntry[] { };
                            }
                            if (data.exportVersion == 2)
                            {
                                matteRead = inputReadNodes.Select(entry => entry.node).First();
                                cutteeReads = inputReadNodes.Select(entry => entry.node).Skip(1).ToList();
                            }
                            else
                            {
                                matteRead = inputReadNodes.Where(entry => entry.port == 1).Select(entry => entry.node).FirstOrDefault();
                                cutteeReads = inputReadNodes.Where(entry => entry.port == 0).Select(entry => entry.node).ToList();
                            }
                            // Don't render mattes tranditionally
                            foreach (var instantiatedNode in idToInstantiatedNode[matteRead.id])
                                instantiatedNode.gameObject.GetComponentInChildren<SpriteRenderer>().enabled = matteRead.visible.HasValue ? matteRead.visible.Value : false;
                            // Only accept first matte entry, shader can't accept more currently
                            return cutteeReads.Select(cuttee => new CutterEntry
                            {
                                cuttee = idToInstantiatedNode[cuttee.id].First().gameObject.GetComponentInChildren<SpriteRenderer>(),
                                matte = idToInstantiatedNode[matteRead.id].First().gameObject.GetComponentInChildren<SpriteRenderer>(),
                                inverse = inverse,
                            });
                        });
                    var spriteSheetsInfo = spriteSheetToSprites
                        .Select(entry =>
                        {
                            var splits = entry.Key.Split('-');
                            return new
                            {
                                resolutionName = splits[0],
                                paletteName = String.Join("-", splits.Skip(1)),
                                sprites = entry.First().Select(sprite => sprite.Value).ToArray(),
                            };
                        })
                        .ToLookup(entry => entry.resolutionName, entry => new { entry.paletteName, entry.sprites })
                        .Select(entry => new TBGStore.ResolutionInfo
                        {
                            ResolutionName = entry.Key,
                            Palettes = entry
                                .Select(element => new TBGStore.PaletteInfo
                                {
                                    PaletteName = element.paletteName,
                                    Sprites = element.sprites,
                                })
                                .ToArray(),
                        })
                        .ToArray();
                    tbgStore = ScriptableObject.CreateInstance<TBGStore>();
                    {
                        tbgStore.name = $"{assetName} Store";
                        tbgStore.Material = material;
                        tbgStore.Resolutions = spriteSheetsInfo;
                        tbgStore.SpriteNames = spriteSheetToSprites.First().First().Select(spriteEntry => spriteEntry.Key).ToArray();
                        tbgStore.Metadata = stages.First().metadata
                            .Select(meta => new TBGStore.MetadataEntry
                            {
                                Name = meta.name,
                                Value = meta.value,
                                Node = meta.node,
                            })
                            .ToArray();
                        tbgStore.SkinGroups = groupIDToSkinID
                            .Where(entry => entry.Key > 0)
                            .Select(entry => new TBGStore.SkinGroupInfo
                            {
                                GroupName = groupIDToName[entry.Key],
                                SkinNames = entry.Value
                                    .Select(skinID => skinIDToName[skinID])
                                    .ToArray(),
                            })
                            .ToArray();
                        tbgStore.CutterToMatteReadIndex = cutterEntries
                            .Select(entry => rendererToReadIndex[entry.matte].FirstOrDefault())
                            .ToArray();
                        tbgStore.CutterToCutteeReadIndex = cutterEntries
                            .Select(entry => rendererToReadIndex[entry.cuttee].FirstOrDefault())
                            .ToArray();
                        tbgStore.CutterToInverse = cutterEntries
                            .Select(entry => entry.inverse)
                            .ToArray();
                        tbgStore.OnEnable();
                    };
                    ctx.AddObjectToAsset("TBG Store", tbgStore);
                    using (var initBlock = prefab.AddComponentAndInit<TBGRenderer.TBGRenderer>())
                    {
                        tbgRenderer = initBlock.component;
                        tbgRenderer.ReadToSpriteRenderers = readToSpriteRenderers;
                        tbgRenderer.Store = tbgStore;
                        tbgRenderer.GroupToSkinID = stages.First().groups
                            .Where(group => group.groupId > 0)
                            .Select(group => (ushort)1)
                            .ToArray();
                        tbgRenderer.CutterToTransform = cutterEntries.Select(entry => entry.matte.gameObject.transform).ToArray();
                        tbgRenderer.SkinGroups = groupIDToSkinID
                            .Where(entry => entry.Key > 0)
                            .Select(groupEntry => new TBGRenderer.TBGRenderer.SkinGroup
                            {
                                Skins = groupEntry.Value
                                    .Select(skinID => new TBGRenderer.TBGRenderer.Skin
                                    {
                                        SpriteRenderers = nodeToSkinToSpriteRenderer
                                            .Where(entry => nodeToSkinGroup[entry.Key].groupID == groupEntry.Key)
                                            .SelectMany(entry => entry.Value)
                                            .Where(entry => entry.Key == skinID)
                                            .Select(entry => entry.Value)
                                            .ToArray(),
                                    })
                                    .ToArray(),
                            })
                            .ToArray();
                    }
                }

                // Generate special transform heirarchy for skewing
                Dictionary<int, SkewTransforms> nodeToSkewTransforms;
                {
                    var skewNodes = data.animations
                        .SelectMany(animation => animation.Value.attrlinks
                            .Where(attrlink => attrlink.attr == "skew")
                            .Select(attrlink => attrlink.node))
                        .Distinct();
                    var nodeNameToIDs = skeleton.nodes.ToLookup(entry => entry.name, entry => entry.id);
                    var skewEntries = skewNodes
                        .SelectMany(nodeName => nodeToInstantiated[nodeName])
                        .Select(instantiated =>
                        {
                            var existingChildren = Enumerable
                                .Range(0, instantiated.gameObject.transform.childCount)
                                .Select(index => instantiated.gameObject.transform.GetChild(index))
                                .ToList();
                            var skewBase = new GameObject(instantiated.name + "_SkewBase").transform;
                            var skewCounter = new GameObject(instantiated.name + "_SkewCounter").transform;
                            skewBase.parent = instantiated.gameObject.transform;
                            skewBase.localPosition = Vector3.zero;
                            skewBase.localRotation = Quaternion.AngleAxis(45, Vector3.forward);
                            skewCounter.parent = skewBase;
                            skewCounter.localPosition = Vector3.zero;
                            skewCounter.localRotation = Quaternion.AngleAxis(-45, Vector3.forward);
                            foreach (var existingChild in existingChildren)
                            {
                                existingChild.parent = skewCounter;
                            }
                            return new { instantiated.id, skewBase, skewCounter };
                        })
                        .ToList();
                    nodeToSkewTransforms = skewEntries.ToDictionary(entry => entry.id, entry => new SkewTransforms
                    {
                        skewBase = entry.skewBase,
                        skewCounter = entry.skewCounter,
                    });
                }

                if (AnimatorController == null && CreateAnimatorController)
                {
                    var name = $"{assetName} Animator Controller";
                    var path = Path.Combine(
                        Path.GetDirectoryName(ctx.assetPath),
                        name + ".controller");
                    AnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    if (AnimatorController == null)
                    {
                        AnimatorController = AnimatorController.CreateAnimatorControllerAtPath(path);
                        AnimatorController.name = name;
                    }
                }
                animator.runtimeAnimatorController = AnimatorController;

                if (SpriteAtlas == null && CreateSpriteAtlas)
                {
                    var name = $"{assetName} Atlas";
                    var path = Path.Combine(Path.GetDirectoryName(ctx.assetPath), $"{name}.asset");
                    SpriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    if (SpriteAtlas == null)
                    {
                        SpriteAtlas = new SpriteAtlas
                        {
                            name = name
                        };
                        SpriteAtlas.SetPackingSettings(new SpriteAtlasPackingSettings
                        {
                            enableTightPacking = false, // Tight-packing disables cutters.
                        });
                        SpriteAtlas.SetPlatformSettings(new TextureImporterPlatformSettings
                        {
                            maxTextureSize = 8192,
                        });
                        SpriteAtlas.SetTextureSettings(new SpriteAtlasTextureSettings
                        {
                            filterMode = FilterMode.Bilinear,
                            generateMipMaps = true,
                        });
                        AssetDatabase.CreateAsset(SpriteAtlas, path);
                    }
                }

                // HACK - AnimatorController state clips have to be touched for them to be seen in the Animation window.
                if (AnimatorController != null)
                {
                    foreach (var state in AnimatorController.layers[0].stateMachine.states)
                    {
                        state.state.motion = state.state.motion;
                    }
                }

                // HACK - SpriteAtlas can hang on to old data about sprites that isn't reflected in the project. Remove all 'null' elements that are now invalid references.
                if (SpriteAtlas != null)
                {
                    SpriteAtlas.Remove(new UnityEngine.Object[] { null });
                }

                Profiler.BeginSample("Animation Building");
                {
                    var inToOut = skeleton.links.ToLookup(entry => entry.nodeIn, entry => entry.nodeOut);
                    var outToIn = skeleton.links.ToLookup(entry => entry.nodeOut, entry => entry.nodeIn);
                    var nodeIDToName = skeleton.nodes.ToDictionary(entry => entry.id, entry => entry.name);
                    var nodeNameToIDs = skeleton.nodes.ToLookup(entry => entry.name, entry => entry.id);
                    var nodeToInstantiatedGameObjects = instantiatedNodes
                        .ToLookup(entry => entry.id, entry => entry.gameObject);
                    var nodeToInstances = nodeToInstantiatedGameObjects
                        .SelectMany(entry => entry
                            .Select(gameObject => new { Key = nodeIDToName[entry.Key], Value = new NodeInstance { name = nodeIDToName[entry.Key], transform = gameObject.transform } }))
                        .ToLookup(entry => entry.Key, entry => entry.Value);
                    var nodeToChildInstances = nodeToInstantiatedGameObjects
                        .SelectMany(entry => inToOut[entry.Key]
                            .SelectMany(child => nodeToInstantiatedGameObjects[child]
                                .Select(gameObject => new { Key = nodeIDToName[entry.Key], Value = new NodeInstance { name = nodeIDToName[child], transform = gameObject.transform } })))
                        .ToLookup(entry => entry.Key, entry => entry.Value);
                    var readNodeToAffectingBones = instantiatedBones
                        .SelectMany(entry => new Transform[] { entry.gameObject.transform }
                            .Concat(nodeNameToIDs[entry.readName]
                                .SelectMany(node => nodeToInstantiatedGameObjects[node].Select(gameObject => gameObject.transform)))
                            .Select(transform => new
                            {
                                entry.readName,
                                transform,
                            }))
                        .ToLookup(entry => entry.readName, entry => entry.transform);
                    var clips = stages
                        .Select(stage =>
                        {
                            var clipBuilderSettings = new TBGClipBuilderSettings
                            {
                                Name = stage.play.animation,
                                Framerate = Framerate,
                                Stepped = Stepped,
                                RootGameObject = prefab,
                                Skeleton = skeleton,
                                Animation = data.animations[stage.play.animation],
                                DrawingAnimation = data.drawingAnimations[stage.play.drawingAnimation],
                                NodeToInstantiated = nodeToInstantiatedGameObjects,
                                NodeNameToIDs = nodeNameToIDs,
                                NodeIDToName = nodeIDToName,
                                OutToIn = outToIn,
                            };

                            // Core Animation Pass.
                            {
                                var clipBuilder = new TBGClipBuilder
                                {
                                    Settings = clipBuilderSettings,
                                    AttributesToSplit3D = new HashSet<string> {
                                        "position",
                                        "offset",
                                    },
                                    AttributeToProperty = new Dictionary<string, string> {
                                        { "rotation.anglez", "localEulerAnglesRaw.z" },
                                        { "position.x", "m_LocalPosition.x" },
                                        { "position.y", "m_LocalPosition.y" },
                                        { "offset.x", "m_LocalPosition.x" },
                                        { "offset.y", "m_LocalPosition.y" },
                                    },
                                    AttributeToNodeToValueMap = new Dictionary<string, NodeToValueMap> {
                                        { "rotation.anglez", node => value => value * Mathf.Rad2Deg },
                                    },
                                }
                                .WithSpriteOrderCurves(node => readNodeToAffectingBones[node])
                                .WithTimedValueCurves();
                            }

                            // Scale / Skew Pass.
                            {
                                var nodeToSkewTransform = nodeNameToIDs
                                    .SelectMany(entry => entry
                                        .Select(nodeID => new
                                        {
                                            Key = entry.Key,
                                            NodeID = nodeID,
                                            transforms = nodeToSkewTransforms.TryGetValue(nodeID, out var skewTransforms) ? skewTransforms : null
                                        })
                                        .Where(entry => entry.transforms != null))
                                    .ToLookup(entry => entry.Key, entry => entry);
                                var clipBuilder = new TBGClipBuilder
                                {
                                    Settings = clipBuilderSettings,
                                    AttributesToSplit3D = new HashSet<string> {
                                        "scale",
                                    },
                                    AttributeToAdvancedNodeMappings = new Dictionary<string, AdvancedNodeMapping[]> {
                                        { "scale.x", new AdvancedNodeMapping[]{ new AdvancedNodeMapping {
                                            nodeToInstance = node => nodeToSkewTransform[node].Select(entry => new NodeInstance { name = entry.Key, transform = entry.transforms.skewCounter}),
                                            propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                { "m_LocalScale.x", new NodeValueTransform { nodeToValueMap = node => value => value } },
                                            },
                                        } } },
                                        { "scale.y", new AdvancedNodeMapping[]{ new AdvancedNodeMapping {
                                            nodeToInstance = node => nodeToSkewTransform[node].Select(entry => new NodeInstance { name = entry.Key, transform = entry.transforms.skewCounter}),
                                            propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                { "m_LocalScale.y", new NodeValueTransform { nodeToValueMap = node => value => value } },
                                            },
                                        } } },
                                        { "skew",  new AdvancedNodeMapping[]{ new AdvancedNodeMapping {
                                            nodeToInstance = node => nodeToSkewTransform[node].Select(entry => new NodeInstance {name = entry.Key, transform= entry.transforms.skewBase}),
                                            propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                { "m_LocalScale.x", new NodeValueTransform { nodeToValueMap =  _ => value =>
                                                    Mathf.Sqrt((Mathf.Pow(Mathf.Cos(value * Mathf.Deg2Rad), 2) + Mathf.Pow(1 + Mathf.Sin(value * Mathf.Deg2Rad), 2)) / 2.0f) } },
                                                { "m_LocalScale.y",  new NodeValueTransform { nodeToValueMap = _ => value =>
                                                    Mathf.Sqrt((Mathf.Pow(Mathf.Cos(value * Mathf.Deg2Rad), 2) + Mathf.Pow(1 - Mathf.Sin(value * Mathf.Deg2Rad), 2)) / 2.0f) } },
                                                { "localEulerAnglesRaw.z",  new NodeValueTransform { nodeToValueMap = _ => value => 45 - value / 2.0f } },
                                            }
                                        } } },
                                    },
                                }
                                .WithTimedValueCurves();
                            }

                            // Pivot Pass.
                            {
                                var clipBuilder = new TBGClipBuilder
                                {
                                    Settings = clipBuilderSettings,
                                    AttributesToSplit3D = new HashSet<string> {
                                        "pivot",
                                    },
                                    AttributeToAdvancedNodeMappings = new Dictionary<string, AdvancedNodeMapping[]> {
                                        { "pivot.x", new AdvancedNodeMapping[]{
                                            new AdvancedNodeMapping
                                            {
                                                nodeToInstance = node => nodeToInstances[node],
                                                propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                    { "m_LocalPosition.x", new NodeValueTransform {
                                                        nodeToValueMap = node => value => value,
                                                        blendFunction = (a, b) => a + b,
                                                    }
                                                } }
                                            },
                                            new AdvancedNodeMapping
                                            {
                                                nodeToInstance = node => nodeToChildInstances[node],
                                                propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                    { "m_LocalPosition.x", new NodeValueTransform {
                                                        nodeToValueMap = node => value => -value,
                                                        blendFunction = (a, b) => a + b,
                                                    } },
                                                }
                                            }
                                        } },
                                        { "pivot.y", new AdvancedNodeMapping[]{
                                            new AdvancedNodeMapping
                                            {
                                                nodeToInstance = node => nodeToInstances[node],
                                                propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                    { "m_LocalPosition.y", new NodeValueTransform {
                                                        nodeToValueMap = node => value => value,
                                                        blendFunction = (a, b) => a + b,
                                                    }
                                                } }
                                            },
                                            new AdvancedNodeMapping
                                            {
                                                nodeToInstance = node => nodeToChildInstances[node],
                                                propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                    { "m_LocalPosition.y", new NodeValueTransform {
                                                        nodeToValueMap = node => value => -value,
                                                        blendFunction = (a, b) => a + b,
                                                    } },
                                                }
                                            }
                                        } },
                                    },
                                }
                                    .WithTimedValueCurves();
                            }

                            // Deform Pass.
                            {
                                var finalBoneEndInstances = new HashSet<string>(finalBoneEnds.Select(entry => entry.Value.name));
                                var nodeToInstantiatedBoneGameObjects = instantiatedBones
                                    .ToLookup(entry => entry.boneName, entry => entry.gameObject);
                                var readBoneToBoneRestInfo = nodeToInstantiatedBoneGameObjects
                                    .SelectMany(entry => entry.Select(readBone => new { readBone.name, boneRestInfo = nodeToBoneRestInfo[entry.Key] }))
                                    .ToDictionary(entry => entry.name, entry => entry.boneRestInfo);
                                var readBoneToChildBoneRestInfo = nodeToBoneRestInfo
                                    .SelectMany(entry => nodeToInstantiatedBoneGameObjects[nodeIDToName[entry.Value.parentNode]]
                                        .Select(readBone => new { readBone.name, boneRestInfo = entry.Value }))
                                    .ToLookup(entry => entry.name, entry => entry.boneRestInfo);
                                var nodeToInstantiatedBonesEntries = nodeToRestInfo
                                    .SelectMany(entry => nodeToInstantiatedBoneGameObjects[entry.Key]
                                        .Select(instantiatedBone => new { entry.Key, Value = new NodeInstance { name = instantiatedBone.name, transform = instantiatedBone.transform } }));
                                var nodeToInstantiatedEntries = nodeToRestInfo
                                    .SelectMany(entry => nodeNameToIDs[entry.Key]
                                        .SelectMany(nodeName => inToOut[nodeName]
                                            .SelectMany(child => nodeToInstantiatedGameObjects[child]
                                                .Select(gameObject => new { entry.Key, Value = new NodeInstance { name = nodeIDToName[child], transform = gameObject.transform } }))));
                                var boneToChildTransforms = nodeToInstantiatedBonesEntries
                                    .Concat(nodeToInstantiatedEntries)
                                    .Concat(finalBoneEnds.Select(entry => new { entry.Key, entry.Value }))
                                    .ToLookup(entry => entry.Key, entry => entry.Value);
                                var boneNodeToParentInstantiated = nodeToBoneRestInfo
                                    .ToDictionary(entry => entry.Key, entry =>
                                        nodeToInstantiatedBoneGameObjects[nodeIDToName[entry.Value.parentNode]]
                                        .Select(instantiatedBone => new NodeInstance { name = instantiatedBone.name, transform = instantiatedBone.transform }));
                                var clipBuilder = new TBGClipBuilder
                                {
                                    Settings = clipBuilderSettings,
                                    AttributeToProperty = new Dictionary<string, string> {
                                        { "deform.offset.x", "m_LocalPosition.x" },
                                        { "deform.offset.y", "m_LocalPosition.y" },
                                    },
                                    AttributeToNodeToValueMap = new Dictionary<string, NodeToValueMap> {
                                        { "deform.offset.x", node => value => value + nodeToRestInfo[node].restRootPosition.x },
                                        { "deform.offset.y", node => value => value + nodeToRestInfo[node].restRootPosition.y },
                                    },
                                    AttributesToSplit3D = new HashSet<string> {
                                        "deform.offset",
                                    },
                                    AttributeToAdvancedNodeMappings = new Dictionary<string, AdvancedNodeMapping[]> {
                                        { "deform.rotation.z", new AdvancedNodeMapping[]{ new AdvancedNodeMapping {
                                            propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                { "localEulerAnglesRaw.z", new NodeValueTransform { nodeToValueMap = _ => value => value* Mathf.Rad2Deg } },
                                            }
                                        } } },
                                        { "deform.length", new AdvancedNodeMapping[]{ new AdvancedNodeMapping {
                                            nodeToInstance = node => boneToChildTransforms[node],
                                            propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                { "m_LocalPosition.x", new NodeValueTransform { nodeToValueMap = node => value =>
                                                    nodeToRestInfo.TryGetValue(node, out var result)
                                                        ? value + result.restRootPosition.x
                                                        : finalBoneEndInstances.Contains(node) ? value : 0 } },
                                                { "m_LocalPosition.y", new NodeValueTransform { nodeToValueMap = node => _ =>
                                                    nodeToRestInfo.TryGetValue(node, out var result) ? result.restRootPosition.y : 0 } },
                                                { "m_LocalScale.x", new NodeValueTransform { nodeToValueMap = node => value =>
                                                    readBoneToBoneRestInfo.TryGetValue(node, out var result) ? value / result.restLength : 1 } },
                                            }
                                        } } },
                                        { "deform.radius", new AdvancedNodeMapping[]{
                                            new AdvancedNodeMapping
                                            {
                                                nodeToInstance = node => nodeToInstantiatedBoneGameObjects[node].Select(instantiatedBone =>
                                                    new NodeInstance {name=instantiatedBone.name, transform=instantiatedBone.transform}),

                                                propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                    { "m_LocalPosition.x", new NodeValueTransform {
                                                        nodeToValueMap = node => value => readBoneToBoneRestInfo.TryGetValue(node, out var restInfo) ? value - restInfo.restRadius : 0,
                                                    } },
                                                    { "m_LocalScale.x",  new NodeValueTransform
                                                    {
                                                        nodeToValueMap = node => value =>
                                                            readBoneToBoneRestInfo.TryGetValue(node, out var result) ? (result.restLength + result.restRadius - value) / result.restLength : 1,
                                                        blendFunction = (a, b) => (a - 1) + (b - 1) + 1,
                                                    } },
                                                }
                                            },
                                            new AdvancedNodeMapping
                                            {
                                                nodeToInstance = node => boneNodeToParentInstantiated[node],
                                                propertyToNodeValueTransform = new Dictionary<string, NodeValueTransform> {
                                                    { "m_LocalScale.x", new NodeValueTransform {
                                                        nodeToValueMap = node => value => {
                                                            var result = readBoneToChildBoneRestInfo[node];
                                                            if (!result.Any()) return 1;
                                                            var childRestInfo = result.First();
                                                            return  (childRestInfo.restLength + childRestInfo.restRadius - value) / childRestInfo.restLength;
                                                        },
                                                        blendFunction = (a, b) => (a - 1) + (b - 1) + 1,
                                                    } }
                                                }
                                            }
                                        } },
                                    },
                                }
                                    .WithTimedValueCurves()
                                    .WithDrawingAnimationCurves(nodeToSkinToSpriteRenderer, spriteSheetToSprites.First().First())
                                    .ApplyRegisteredCurvesToClip();
                            }

                            ctx.AddObjectToAsset(clipBuilderSettings.clip.name, clipBuilderSettings.clip);
                            return new { clipBuilderSettings.clip, clipBuilderSettings };
                        })
                        .ToList();
                    Profiler.EndSample();

                    if (clips.Count() > 0)
                    {
                        TBGClipBuilder.ApplyAnimationClip(
                            settings: clips[0].clipBuilderSettings,
                            rootTransform: prefab.transform,
                            frame: 0,
                            fieldMap: new Dictionary<string, string>{
                                {"localEulerAnglesRaw", "localEulerAngles"},
                                {"m_LocalPosition", "localPosition" },
                                {"m_LocalScale", "localScale" },
                            });
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                if (prefab != null)
                {
                    DestroyImmediate(prefab);
                }
            }
        }

        public static bool IsCutter(NodeSettings node)
        {
            return node.tag == "cutter" || node.tag == "inverseCutter";
        }

        private static GameObject CreateGameObject(NodeSettings node, HashSet<string> usedNames, Transform parent)
        {
            var gameObjectName = node.name;
            for (var nameIndex = 1; usedNames.Contains(gameObjectName); nameIndex++)
            {
                gameObjectName = $"{node.name}-{nameIndex}";
            }
            usedNames.Add(gameObjectName);
            GameObject gameObject = new GameObject(gameObjectName);
            gameObject.transform.parent = parent;
            gameObject.transform.localPosition = Vector3.zero;
            return gameObject;
        }
    }
}

#endif
