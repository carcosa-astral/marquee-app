using Marquee.Core.Storage;

namespace Marquee.Core.Tests;

public class MarqueeFolderTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Root_is_the_folder_that_holds_the_executable()
    {
        var dir = Path.Combine(Temp(), "Marquee");

        var folder = MarqueeFolder.FromExecutableDirectory(dir + Path.DirectorySeparatorChar);

        Assert.Equal(dir, folder.Root);
        Assert.Equal(Path.Combine(dir, "data", "marquee.db"), folder.Database);
        Assert.Equal(Path.Combine(dir, "data", "images"), folder.Images);
        Assert.Equal(Path.Combine(dir, "backups"), folder.Backups);
        Assert.Equal(Path.Combine(dir, "exports"), folder.Exports);
    }

    [Fact]
    public void Mac_app_bundle_uses_the_folder_next_to_the_bundle()
    {
        var marquee = Path.Combine(Temp(), "Marquee");
        var exeDir = Path.Combine(marquee, "Marquee.app", "Contents", "MacOS");

        var folder = MarqueeFolder.FromExecutableDirectory(exeDir);

        Assert.Equal(marquee, folder.Root);
    }

    [Fact]
    public void A_folder_named_MacOS_outside_a_bundle_is_left_alone()
    {
        var exeDir = Path.Combine(Temp(), "Contents", "MacOS");

        var folder = MarqueeFolder.FromExecutableDirectory(exeDir);

        Assert.Equal(exeDir, folder.Root);
    }

    [Fact]
    public void EnsureCreated_makes_every_folder_inside_the_root()
    {
        var folder = MarqueeFolder.AtRoot(Temp());
        try
        {
            folder.EnsureCreated();

            Assert.True(Directory.Exists(folder.Data));
            Assert.True(Directory.Exists(folder.Images));
            Assert.True(Directory.Exists(folder.Backups));
            Assert.True(Directory.Exists(folder.Exports));
            Assert.All([folder.Data, folder.Images, folder.Backups, folder.Exports], p => Assert.True(folder.Contains(p)));
        }
        finally
        {
            Directory.Delete(folder.Root, recursive: true);
        }
    }

    [Fact]
    public void Contains_rejects_sibling_folders_with_a_shared_prefix()
    {
        var root = Temp();
        var folder = MarqueeFolder.AtRoot(Path.Combine(root, "Marquee"));

        Assert.True(folder.Contains(Path.Combine(root, "Marquee", "data")));
        Assert.False(folder.Contains(Path.Combine(root, "Marquee2", "data")));
        Assert.False(folder.Contains(root));
    }
}
