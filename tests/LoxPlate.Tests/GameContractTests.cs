using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LoxPlate.Tests;

/// <summary>
/// Loads the compiled mod DLL plus the real 1.0+ game assemblies
/// reflection-only (MetadataLoadContext - no Unity runtime needed) and pins
/// every game-API assumption LoxPlate makes. A failure here means a game
/// update broke the mod's contract: re-verify the sources before shipping.
///
/// Two metadata-load quirks shape the assertions: reflected System.Type
/// instances are not reference-equal to the test runtime's typeof(...), so
/// signatures are compared by type Name; and AccessTools.Field lookups fail
/// silently at runtime, so every reflection-string the mod depends on is a
/// hard test failure here.
/// </summary>
public class GameContractTests
{
    private static readonly Lazy<Assemblies> Asms = new(() => new Assemblies());

    private sealed class Assemblies : IDisposable
    {
        private readonly MetadataLoadContext _mlc;
        public Assembly Game { get; }
        public Assembly GameUtils { get; }
        public Assembly Mod { get; }
        public IReadOnlyList<Assembly> All { get; }
        public string RepoRoot { get; }
        public byte[] ModBytes { get; }

        public Assemblies()
        {
            RepoRoot = FindRepoRoot();
            string managed = Path.Combine(RepoRoot, ".cache", "valheim-server", "valheim_server_Data", "Managed");
            string refs = Path.Combine(RepoRoot, "refs");
            string libs = Path.Combine(RepoRoot, "libs");
            string modDll = Path.Combine(RepoRoot, "src", "bin", "Release", "LoxPlate.dll");

            if (!File.Exists(modDll))
            {
                throw new XunitException($"Mod DLL not found at {modDll} - run scripts/build.sh first");
            }

            var paths = new List<string>();
            if (Directory.Exists(managed))
            {
                paths.AddRange(Directory.EnumerateFiles(managed, "*.dll"));
            }
            if (Directory.Exists(refs))
            {
                paths.AddRange(Directory.EnumerateFiles(refs, "*.dll"));
            }
            if (Directory.Exists(libs))
            {
                paths.AddRange(Directory.EnumerateFiles(libs, "*.dll"));
            }
            paths.Add(modDll);
            paths = paths.Distinct().ToList();

            _mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), coreAssemblyName: "mscorlib");
            Game = _mlc.LoadFromAssemblyPath(Path.Combine(refs, "assembly_valheim.dll"));
            // MetadataLoadContext resolves lazily; force-load the utils
            // assembly so its types (Utils, StringExtensionMethods) are
            // visible to the fallback scan below.
            GameUtils = _mlc.LoadFromAssemblyPath(Path.Combine(refs, "assembly_utils.dll"));
            // AssetBundle lives in its own module (used by the loxplate.assets loader).
            _mlc.LoadFromAssemblyPath(Path.Combine(refs, "UnityEngine.AssetBundleModule.dll"));
            Mod = _mlc.LoadFromAssemblyPath(modDll);
            ModBytes = File.ReadAllBytes(modDll);
            All = _mlc.GetAssemblies().ToList();
        }

        public void Dispose() => _mlc.Dispose();

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "scripts")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new XunitException("Could not locate repository root above " + AppContext.BaseDirectory);
        }
    }

    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static;

    private static Type GameType(string name)
    {
        Type type = Asms.Value.Game.GetType(name, throwOnError: false)
            ?? Asms.Value.GameUtils.GetType(name, throwOnError: false);
        if (type != null)
        {
            return type;
        }
        // Anything else referenced by the mod (e.g. in assembly_guiutils).
        type = Asms.Value.All.FirstOrDefault(a => a.GetType(name, throwOnError: false) != null)?.GetType(name);
        return type ?? throw new XunitException($"Game type {name} missing from all game assemblies");
    }

    private static MethodInfo? FindMethod(Type type, string name, params string[] parameterTypeNames)
    {
        return type.GetMethods(Any).FirstOrDefault(m => m.Name == name
            && m.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(parameterTypeNames));
    }

    private static void RequireMethod(Type type, string name, params string[] parameterTypeNames)
    {
        string signature = $"{type.Name}.{name}({string.Join(", ", parameterTypeNames)})";
        Assert.True(FindMethod(type, name, parameterTypeNames) != null, $"Game method {signature} missing");
    }

    private static void RequireField(Type type, string name, bool nonPublic = true)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public
            | (nonPublic ? BindingFlags.NonPublic : 0) | BindingFlags.Instance | BindingFlags.Static);
        Assert.True(field != null, $"Game field {type.Name}.{name} missing");
    }

    // ------------------------------------------------------------------
    // Interaction contract: the saddle-style UseItem flow LoxPlate hijacks.
    // ------------------------------------------------------------------

    [Fact]
    public void Tameable_UseItem_TakesHumanoidAndItemData()
    {
        MethodInfo method = FindMethod(GameType("Tameable"), "UseItem", "Humanoid", "ItemData");
        Assert.True(method != null, "Tameable.UseItem(Humanoid, ItemDrop.ItemData) missing");
        Assert.Equal("Boolean", method!.ReturnType.Name);
    }

    [Fact]
    public void Tameable_Interact_And_Hover_Exist()
    {
        Type tameable = GameType("Tameable");
        RequireMethod(tameable, "Interact", "Humanoid", "Boolean", "Boolean");
        RequireMethod(tameable, "GetHoverText");
        RequireMethod(tameable, "GetHoverName");
        RequireMethod(tameable, "IsTamed");
    }

    [Fact]
    public void Character_IsCrouching_Exists()
    {
        RequireMethod(GameType("Character"), "IsCrouching");
    }

    // ------------------------------------------------------------------
    // Damage contract.
    // ------------------------------------------------------------------

    [Fact]
    public void Character_ApplyDamage_Signature_Matches()
    {
        RequireMethod(GameType("Character"), "ApplyDamage", "HitData", "Boolean", "Boolean", "DamageModifier");
    }

    [Fact]
    public void HitData_ModifierHelpers_Exist()
    {
        Type hitData = GameType("HitData");
        RequireMethod(hitData, "ApplyModifier", "Single");
        MethodInfo getTotal = FindMethod(hitData, "GetTotalDamage");
        Assert.True(getTotal != null, "HitData.GetTotalDamage missing");
        Assert.Equal("Single", getTotal!.ReturnType.Name);
    }

    [Fact]
    public void DamageText_ShowText_Broadcast_Exists()
    {
        // Absorbed hits show their soaked amount as an immune-style number;
        // ShowText routes to every peer, so server-side calls reach the shooter.
        RequireMethod(GameType("DamageText"), "ShowText", "DamageModifier", "Vector3", "Single", "Boolean");
    }

    [Fact]
    public void AssetBundle_Loading_Surface_Exists()
    {
        MethodInfo loadFromFile = FindMethod(GameType("UnityEngine.AssetBundle"), "LoadFromFile", "String");
        Assert.True(loadFromFile != null, "AssetBundle.LoadFromFile(string) missing");
        Assert.True(loadFromFile!.ReturnType.Name == "AssetBundle", "LoadFromFile return type changed");
    }

    [Fact]
    public void Character_SpeedFactors_And_Death_Exist()
    {
        Type character = GameType("Character");
        MethodInfo jog = FindMethod(character, "GetJogSpeedFactor");
        MethodInfo run = FindMethod(character, "GetRunSpeedFactor");
        Assert.True(jog != null && jog.ReturnType.Name == "Single", "Character.GetJogSpeedFactor() changed");
        Assert.True(run != null && run.ReturnType.Name == "Single", "Character.GetRunSpeedFactor() changed");
        MethodInfo onDeath = FindMethod(character, "OnDeath");
        Assert.True(onDeath != null && onDeath.ReturnType.Name == "Void", "Character.OnDeath() changed");
        RequireMethod(character, "GetCenterPoint");
    }

    [Fact]
    public void CharacterDrop_DropItems_Signature_Matches()
    {
        MethodInfo dropItems = FindMethod(GameType("CharacterDrop"), "DropItems",
            "List`1", "Vector3", "Single", "Boolean");
        Assert.True(dropItems != null,
            "CharacterDrop.DropItems(List<KeyValuePair<GameObject,int>>, Vector3, float, bool) changed");
        Assert.True(dropItems!.IsStatic, "DropItems must stay static");
    }

    // ------------------------------------------------------------------
    // Persistence contract: ZDO state and the world clock used for regen.
    // ------------------------------------------------------------------

    [Fact]
    public void ZNet_GetTimeSeconds_ReturnsDouble()
    {
        MethodInfo getTime = FindMethod(GameType("ZNet"), "GetTimeSeconds");
        Assert.True(getTime != null && getTime.ReturnType.Name == "Double",
            "ZNet.GetTimeSeconds() changed");
    }

    [Fact]
    public void ZDo_IntAndFloatAccessors_Exist()
    {
        Type zdo = GameType("ZDO");
        RequireMethod(zdo, "GetInt", "Int32", "Int32");
        RequireMethod(zdo, "GetFloat", "Int32", "Single");
        // The int overload carries an optional okForNotOwner flag.
        RequireMethod(zdo, "Set", "Int32", "Int32", "Boolean");
        RequireMethod(zdo, "Set", "Int32", "Single");
    }

    [Fact]
    public void ZNetView_RpcSurface_Exists()
    {
        Type netView = GameType("ZNetView");
        RequireMethod(netView, "IsValid");
        RequireMethod(netView, "IsOwner");
        RequireMethod(netView, "GetZDO");
        Assert.Contains(netView.GetMethods(Any),
            m => m.Name == "Register" && m.IsGenericMethod);
        Assert.Contains(netView.GetMethods(Any), m => m.Name == "InvokeRPC"
            && m.GetParameters().FirstOrDefault()?.ParameterType.Name == "String");
    }

    [Fact]
    public void ObjectDb_ItemStorage_Fields_Exist()
    {
        Type objectDb = GameType("ObjectDB");
        RequireField(objectDb, "m_items", nonPublic: false);
        RequireField(objectDb, "m_itemByHash", nonPublic: true);
        RequireField(objectDb, "m_recipes", nonPublic: false);
        Assert.True(FindMethod(objectDb, "GetItemPrefab", "String") != null,
            "ObjectDB.GetItemPrefab(string) missing");
    }

    [Fact]
    public void ZNetScene_PrefabStorage_Fields_Exist()
    {
        Type netScene = GameType("ZNetScene");
        RequireField(netScene, "m_prefabs", nonPublic: false);
        RequireField(netScene, "m_namedPrefabs", nonPublic: true);
    }

    // ------------------------------------------------------------------
    // Item/recipe construction contract.
    // ------------------------------------------------------------------

    [Fact]
    public void ItemDrop_SharedData_Fields_Exist()
    {
        Type shared = GameType("ItemDrop+ItemData+SharedData");
        RequireField(shared, "m_name", nonPublic: false);
        RequireField(shared, "m_description", nonPublic: false);
        RequireField(shared, "m_icons", nonPublic: false);
        RequireField(shared, "m_weight", nonPublic: false);
        RequireField(shared, "m_maxStackSize", nonPublic: false);
        FieldInfo itemType = shared.GetField("m_itemType", BindingFlags.Public | BindingFlags.Instance);
        Assert.True(itemType != null, "SharedData.m_itemType missing");
        Type enumType = itemType!.FieldType;
        Assert.True(enumType.IsEnum && enumType.Name == "ItemType", "m_itemType is no longer an ItemType enum");
        Assert.Contains(enumType.GetFields(BindingFlags.Public | BindingFlags.Static),
            f => f.Name == "Material");
        RequireField(GameType("ItemDrop+ItemData"), "m_dropPrefab", nonPublic: false);
    }

    [Fact]
    public void Recipe_Fields_Exist()
    {
        Type recipe = GameType("Recipe");
        Assert.True(recipe.BaseType?.Name == "ScriptableObject",
            "Recipe must remain a ScriptableObject (CreateInstance<Recipe>() is used)");
        RequireField(recipe, "m_item", nonPublic: false);
        RequireField(recipe, "m_amount", nonPublic: false);
        RequireField(recipe, "m_craftingStation", nonPublic: false);
        RequireField(recipe, "m_minStationLevel", nonPublic: false);
        RequireField(recipe, "m_resources", nonPublic: false);
    }

    [Fact]
    public void Piece_Requirement_Fields_Exist()
    {
        Type requirement = GameType("Piece+Requirement");
        RequireField(requirement, "m_resItem", nonPublic: false);
        RequireField(requirement, "m_amount", nonPublic: false);
        RequireField(requirement, "m_recover", nonPublic: false);
    }

    [Fact]
    public void Inventory_And_Messaging_Helpers_Exist()
    {
        RequireMethod(GameType("Inventory"), "RemoveOneItem", "ItemData");
        // Message is declared on Character with optional amount/icon/log.
        RequireMethod(GameType("Character"), "Message", "MessageType", "String", "Int32", "Sprite", "Boolean");
        RequireMethod(GameType("Utils"), "GetPrefabName", "GameObject");
    }

    // ------------------------------------------------------------------
    // Mod identity: plugin GUID, version, Harmony targets.
    // ------------------------------------------------------------------

    [Fact]
    public void Plugin_Has_Expected_Identity()
    {
        Type plugin = Asms.Value.Mod.GetType("LoxPlate.LoxPlatePlugin", throwOnError: true)!;
        var attributeData = plugin.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == "BepInPlugin");
        Assert.NotNull(attributeData);
        var args = attributeData!.ConstructorArguments.Select(a => a.Value?.ToString()).ToArray();
        Assert.Equal("benjamin.loxplate", args[0]);
        Assert.Equal("LoxPlate", args[1]);
        Assert.Equal("0.1.0", args[2]);
    }

    public static IEnumerable<object[]> PatchTargets()
    {
        yield return new object[] { "ZNetScene", "Awake" };
        yield return new object[] { "ZNet", "Start" };
        yield return new object[] { "ObjectDB", "Awake" };
        yield return new object[] { "ObjectDB", "CopyOtherDB" };
        yield return new object[] { "Tameable", "Awake" };
        yield return new object[] { "Tameable", "UseItem" };
        yield return new object[] { "Tameable", "GetHoverText" };
        yield return new object[] { "Character", "ApplyDamage" };
        yield return new object[] { "Character", "GetJogSpeedFactor" };
        yield return new object[] { "Character", "GetRunSpeedFactor" };
        yield return new object[] { "Character", "OnDeath" };
    }

    [Theory]
    [MemberData(nameof(PatchTargets))]
    public void Every_Harmony_Target_Exists_In_Game(string typeName, string methodName)
    {
        Assemblies asms = Asms.Value;
        Type gameType = asms.Game.GetType(typeName, throwOnError: true)
            ?? throw new XunitException($"Game type {typeName} missing");
        bool declared = asms.Mod.GetTypes().Any(t =>
        {
            if (t.GetCustomAttributesData().All(a => a.AttributeType.Name != "HarmonyPatch"))
            {
                return false;
            }
            foreach (CustomAttributeData attribute in t.GetCustomAttributesData()
                         .Where(a => a.AttributeType.Name == "HarmonyPatch"))
            {
                var args = attribute.ConstructorArguments.Select(a => a.Value).ToArray();
                if (args.Length == 2 && args[0] is Type target && target.Name == typeName
                    && args[1] is string targetMethod && targetMethod == methodName)
                {
                    return true;
                }
            }
            return false;
        });
        Assert.True(declared, $"Mod no longer patches {typeName}.{methodName}");
        Assert.Contains(gameType.GetMethods(Any), m => m.Name == methodName);
    }

    // ------------------------------------------------------------------
    // Saved-data identity: the strings that must never drift. .NET stores
    // string literals in the metadata as UTF-16 and identifier names as
    // UTF-8, so both encodings are scanned.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("lb_tier")]
    [InlineData("lb_pool")]
    [InlineData("lb_lastHit")]
    [InlineData("lb_fit")]
    [InlineData("LoxPlate_BlackMetalBarding")]
    [InlineData("LoxPlate_FlametalBarding")]
    [InlineData("Black Metal Barding")]
    [InlineData("Flametal Barding")]
    [InlineData("GoblinTotem")]
    [InlineData("TrophyCharredMelee")]
    [InlineData("BlackMetal")]
    [InlineData("Flametal")]
    [InlineData("LoxPelt")]
    [InlineData("LinenThread")]
    [InlineData("Forge")]
    [InlineData("loxplate.assets")]
    public void Identity_Strings_Are_Compiled_Into_The_Mod(string expected)
    {
        Assert.True(ContainsEncoded(Asms.Value.ModBytes, expected),
            $"String '{expected}' missing from the compiled mod");
    }

    private static bool ContainsEncoded(byte[] haystack, string needle)
    {
        return Contains(haystack, Encoding.UTF8.GetBytes(needle))
            || Contains(haystack, Encoding.Unicode.GetBytes(needle));
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            for (; j < needle.Length && haystack[i + j] == needle[j]; j++)
            {
            }
            if (j == needle.Length)
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void Mod_Config_Sections_Are_Compiled_In()
    {
        byte[] haystack = Asms.Value.ModBytes;
        foreach (string key in new[]
                 {
                     "General", "BlackMetalBarding", "FlametalBarding", "Pool",
                     "AllowAnyTame", "RecoverOnDeath", "DamageReduction", "HealthPool",
                     "SpeedPenalty", "RegenPerSecond", "RegenDelaySeconds",
                 })
        {
            Assert.True(ContainsEncoded(haystack, key), $"Config key '{key}' missing");
        }
    }

    // ------------------------------------------------------------------
    // Packaging coherence.
    // ------------------------------------------------------------------

    [Fact]
    public void Manifest_Matches_The_Build()
    {
        string repoRoot = Asms.Value.RepoRoot;
        string manifestPath = Path.Combine(repoRoot, "packaging", "manifest.json");
        string manifestText = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(manifestText);
        JsonElement manifest = doc.RootElement;

        Assert.Equal("LoxPlate", manifest.GetProperty("name").GetString());
        Assert.Equal("0.1.0", manifest.GetProperty("version_number").GetString());

        var dependencies = manifest.GetProperty("dependencies").EnumerateArray()
            .Select(d => d.GetString()).ToList();
        Assert.Contains(dependencies, d => d!.StartsWith("denikson-BepInExPack_Valheim-"));
        Assert.Contains(dependencies, d => d!.StartsWith("ValheimModding-Jotunn-"));

        string jotunnVersion = File.ReadAllText(Path.Combine(repoRoot, "libs", "jotunn.version")).Trim();
        Assert.Contains(dependencies, d => d == $"ValheimModding-Jotunn-{jotunnVersion}");

        Assert.True(File.Exists(Path.Combine(repoRoot, "README.md")), "README.md missing");
        Assert.True(File.Exists(Path.Combine(repoRoot, "CHANGELOG.md")), "CHANGELOG.md missing");
    }

    [Fact]
    public void Packaged_Icon_Is_256_Square_Png()
    {
        string iconPath = Path.Combine(Asms.Value.RepoRoot, "packaging", "icon.png");
        byte[] data = File.ReadAllBytes(iconPath);
        Assert.True(data.Length > 8 && data[0] == 0x89 && data[1] == (byte)'P', "icon.png is not a PNG");
        int width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        int height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        Assert.Equal(256, width);
        Assert.Equal(256, height);
    }
}
