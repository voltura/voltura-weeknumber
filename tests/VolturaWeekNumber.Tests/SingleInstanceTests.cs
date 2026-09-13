using VolturaWeekNumber.Platform;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class SingleInstanceTests
{
    [Fact]
    public void IsolatedProfileStartsWhileNormalInstanceMutexExists()
    {
        using var normal = new Mutex(true, @"Local\VolturaWeekNumber", out var created);

        try
        {
            using var isolated = new SingleInstance(NewProfile());

            Assert.True(isolated.IsFirst);
        }
        finally
        {
            if (created)
            {
                normal.ReleaseMutex();
            }
        }
    }

    [Fact]
    public void DifferentIsolatedProfilesRunIndependently()
    {
        using var first = new SingleInstance(NewProfile());
        using var second = new SingleInstance(NewProfile());

        Assert.True(first.IsFirst);
        Assert.True(second.IsFirst);
    }

    [Fact]
    public void EquivalentProfilePathsActivateExistingIsolatedInstance()
    {
        var profile = NewProfile();
        using var first = new SingleInstance(profile);
        using var activated = new ManualResetEventSlim();

        first.Listen(activated.Set);

        using var second = new SingleInstance(profile.ToUpperInvariant() + Path.DirectorySeparatorChar);

        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    private static string NewProfile() => Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
}
