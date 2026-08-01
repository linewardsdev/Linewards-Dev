using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using LTW.Simulation;

namespace LTW.Tests;

/// <summary>
/// Guards the one copy of the simulation that nothing else checks: the pre-built DLL Unity loads.
/// </summary>
/// <remarks>
/// `unity/LTW.UnityClient/Assets/Plugins/LTW.Simulation.dll` is a hand-copied build of
/// `src/LTW.Simulation`. CI formats and tests the SOURCE; the game runs the DLL. Nothing has ever
/// compared them, so a forgotten re-copy means the game plays different rules than the whole test
/// suite verifies — and with two agents committing concurrently, that is a live hazard rather than
/// a theoretical one. See OPEN_ITEMS.md item 22.
///
/// This lives in the test suite rather than as a workflow step so it runs everywhere `dotnet test`
/// already runs, including locally before a commit is made.
/// </remarks>
public sealed class SimulationPluginSyncTests
{
    private const string PluginRelativePath = "unity/LTW.UnityClient/Assets/Plugins/LTW.Simulation.dll";

    /// <summary>
    /// The declared types and members must match — checked, like the IL half, only when the suite
    /// is built in the configuration the committed plugin is.
    /// </summary>
    /// <remarks>
    /// This originally ran in every configuration, on the stated grounds that signatures are
    /// independent of Debug/Release. **They are not, and the committed plugin is a Release build.**
    /// Two things move with configuration and both feed this digest:
    ///
    /// - Compiler-generated types — closure classes, lambda caches, iterator state machines — are
    ///   emitted differently, so walking every TypeDefinition sees a different set.
    /// - Signature blobs encode types as metadata TOKENS, and token values are indices into tables
    ///   whose size depends on how many of those generated types exist. Measured on this assembly:
    ///   1,045 authored members on both sides, names and attributes identical, yet 146 signature
    ///   blobs differing by one or two bytes — `06151259 01 1230` against `06151251 01 1230` — which
    ///   is the same field with the same type at a different token.
    ///
    /// So this check was red in one configuration or the other whatever was committed: a Debug
    /// plugin failed CI, which builds Release, and a Release plugin failed every local run. CI was
    /// in fact red on main from the moment this test landed.
    ///
    /// Filtering the generated members was tried and rejected: it fixes the type-set half but not
    /// the token half, and applying it to the IL digest would stop that digest seeing changes inside
    /// lambda bodies, which carry real logic. Gating on configuration keeps both digests complete.
    ///
    /// The cost is that a local Debug run checks nothing, exactly as the IL half already behaved.
    /// The stronger fix, if local feedback is wanted, is to decode signatures into type NAMES rather
    /// than hashing raw token bytes — genuinely configuration-independent, and a signature decoder
    /// is the price. Signatures are independent of Debug/Release, so this half of the check is meaningful no
    /// matter how the suite was built. It catches a renamed method, a changed parameter list, or a
    /// new command that never reached the client.
    ///
    /// Deliberately covers private members as well as public ones. This is not an API-compatibility
    /// check between two independent components — it is asking whether one file was rebuilt from
    /// the other, and a new private field is just as good an answer as a new public method.
    /// </remarks>
    [Fact]
    public void CommittedUnityPluginDeclaresTheSameMembersAsTheSource()
    {
        if (!IsOptimizedBuild(typeof(AssemblyMarker).Assembly))
        {
            return;
        }

        Assert.Equal(
            Digest(SourceAssemblyPath(), includeMethodBodies: false),
            Digest(CommittedPluginPath(), includeMethodBodies: false));
    }

    /// <summary>
    /// The compiled behaviour must match too, but only Release can say so.
    /// </summary>
    /// <remarks>
    /// A balance change — an income ceiling, a tier multiplier, a targeting rule — moves method
    /// bodies without touching a single signature, so the API check above cannot see it. Comparing
    /// IL can, with one constraint: the C# compiler emits different IL for Debug and Release, so
    /// this is only a valid comparison when the suite itself was built Release, matching the
    /// configuration the committed plugin is built in. CI runs `--configuration Release`, so the
    /// check is enforced exactly where it has to be; a local Debug run skips it rather than
    /// failing spuriously.
    ///
    /// Deliberately compares IL rather than the file bytes. A byte comparison fails on an
    /// in-sync plugin: MVID, the PE header stamp and the PDB id are all build identity, and they
    /// differ between two machines even when every instruction is the same. Measured on the plugin
    /// committed at 184f5e7 — identical 135,680-byte size, identical symbols, 148 differing bytes,
    /// all of them metadata.
    /// </remarks>
    [Fact]
    public void CommittedUnityPluginHasTheSameCompiledBehaviourAsTheSource()
    {
        if (!IsOptimizedBuild(typeof(AssemblyMarker).Assembly))
        {
            return;
        }

        Assert.Equal(
            Digest(SourceAssemblyPath(), includeMethodBodies: true),
            Digest(CommittedPluginPath(), includeMethodBodies: true));
    }

    private static bool IsOptimizedBuild(Assembly assembly) =>
        assembly.GetCustomAttribute<DebuggableAttribute>() is not { IsJITTrackingEnabled: true };

    private static string SourceAssemblyPath() => typeof(AssemblyMarker).Assembly.Location;

    private static string CommittedPluginPath()
    {
        var path = Path.Combine(RepositoryRoot(), PluginRelativePath);
        Assert.True(File.Exists(path), $"Committed Unity plugin not found at {path}.");
        return path;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LTW.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    /// <summary>
    /// A canonical fingerprint of an assembly's declared types and members.
    /// </summary>
    /// <remarks>
    /// Reads metadata directly rather than loading the assembly, because both files declare the
    /// same assembly identity and only one of them can be loaded into a process.
    ///
    /// Entries are sorted before hashing so the fingerprint describes what the assembly declares
    /// rather than the order the compiler happened to emit it in.
    /// </remarks>
    private static string Digest(string assemblyPath, bool includeMethodBodies)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();

        var entries = new List<string>();
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            var typeName = $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
            entries.Add($"T {typeName} {(int)type.Attributes}");

            foreach (var fieldHandle in type.GetFields())
            {
                var field = metadata.GetFieldDefinition(fieldHandle);
                entries.Add(
                    $"F {typeName}.{metadata.GetString(field.Name)} " +
                    $"{(int)field.Attributes} {Convert.ToHexString(metadata.GetBlobBytes(field.Signature))}");
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                var entry = new StringBuilder()
                    .Append("M ").Append(typeName).Append('.').Append(metadata.GetString(method.Name))
                    .Append(' ').Append((int)method.Attributes)
                    .Append(' ').Append(Convert.ToHexString(metadata.GetBlobBytes(method.Signature)));

                if (includeMethodBodies && method.RelativeVirtualAddress != 0)
                {
                    var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
                    entry.Append(' ').Append(Convert.ToHexString(body.GetILBytes() ?? Array.Empty<byte>()));
                }

                entries.Add(entry.ToString());
            }
        }

        entries.Sort(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', entries))));
    }
}
