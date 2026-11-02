using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using PixelPointer.App;

namespace PixelPointer.UnitTests;

public sealed class IpcChannelTests
{
    [Fact]
    public async Task ServeAsync_RejectsIncompleteClientsAndPreservesFragmentedCommands()
    {
        var runtime = Directory.CreateTempSubdirectory("pixelpointer-ipc-");
        var previous = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(runtime.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtime.FullName);
        using var cancellation = new CancellationTokenSource();
        Task? server = null;
        try
        {
            Assert.Null(await IpcChannel.SendControlCommandAsync("status"));
            server = IpcChannel.ServeAsync(_ => { }, () => "running", cancellation.Cancel, cancellation.Token);
            using (var stalled = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await stalled.ConnectAsync(new UnixDomainSocketEndPoint(IpcChannel.SocketPath), TestContext.Current.CancellationToken);
                var buffer = new byte[16];
                Assert.Equal(0, await stalled.ReceiveAsync(buffer, TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            }

            using (var fragmented = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await fragmented.ConnectAsync(new UnixDomainSocketEndPoint(IpcChannel.SocketPath), TestContext.Current.CancellationToken);
                await fragmented.SendAsync("sta"u8.ToArray(), TestContext.Current.CancellationToken);
                await fragmented.SendAsync("tus\n"u8.ToArray(), TestContext.Current.CancellationToken);
                var buffer = new byte[16];
                var count = await fragmented.ReceiveAsync(buffer, TestContext.Current.CancellationToken);
                Assert.Equal("running\n", Encoding.UTF8.GetString(buffer, 0, count));
            }

            Assert.Equal("quitting", await IpcChannel.SendControlCommandAsync("quit"));
            await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                if (server is not null)
                {
                    await server;
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previous);
                runtime.Delete(recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("zoom", true)]
    [InlineData("draw", true)]
    [InlineData("timer", true)]
    [InlineData("status", true)]
    [InlineData("quit", true)]
    [InlineData("ZOOM", false)]
    [InlineData("ping", false)]
    [InlineData("", false)]
    public void IsKnownCommand_AcceptsOnlyDocumentedCommands(string command, bool expected) =>
        Assert.Equal(expected, IpcChannel.IsKnownCommand(command));

    [Fact]
    public void SocketPath_IsNamedAfterProduct() =>
        Assert.Equal("pixelpointer.sock", Path.GetFileName(IpcChannel.SocketPath));

    [Fact]
    public async Task ServeAsync_ProtectsSingleInstanceAndSupportsStatusQuitAndRestart()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("pixelpointer-ipc-");
        var previousRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(runtimeDirectory.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtimeDirectory.FullName);
        using var cancellation = new CancellationTokenSource();
        using var restartCancellation = new CancellationTokenSource();
        Task? server = null;
        Task? restarted = null;
        try
        {
            var received = new ConcurrentQueue<string>();
            server = IpcChannel.ServeAsync(
                command => received.Enqueue(command.Id),
                () => "running",
                cancellation.Cancel,
                cancellation.Token);

            if (OperatingSystem.IsLinux())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(IpcChannel.SocketPath));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                IpcChannel.ServeAsync(_ => { }, () => "running", () => { }, CancellationToken.None));

            Assert.Equal("running", await IpcChannel.SendControlCommandAsync("status"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => IpcChannel.TrySendAsync("status"));
            Assert.True(await IpcChannel.TrySendAsync("zoom"));
            Assert.True(received.TryDequeue(out var command));
            Assert.Equal("zoom", command);
            Assert.Equal("quitting", await IpcChannel.SendControlCommandAsync("quit"));
            await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(File.Exists(IpcChannel.SocketPath));

            restarted = IpcChannel.ServeAsync(
                _ => { },
                () => "running",
                restartCancellation.Cancel,
                restartCancellation.Token);
            Assert.Equal("quitting", await IpcChannel.SendControlCommandAsync("quit"));
            await restarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(File.Exists(IpcChannel.SocketPath));
        }
        finally
        {
            cancellation.Cancel();
            restartCancellation.Cancel();
            try
            {
                if (server is not null)
                {
                    await server;
                }

                if (restarted is not null)
                {
                    await restarted;
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previousRuntimeDirectory);
                runtimeDirectory.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public async Task ServeAsync_DoesNotDeleteNonSocketAtReservedPath()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("pixelpointer-ipc-");
        var previousRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(runtimeDirectory.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtimeDirectory.FullName);
        try
        {
            var path = IpcChannel.SocketPath;
            File.WriteAllText(path, "preserve");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                IpcChannel.ServeAsync(_ => { }, () => "running", () => { }, CancellationToken.None));
            Assert.Equal("preserve", File.ReadAllText(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previousRuntimeDirectory);
            runtimeDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ServeAsync_ReplacesSocketLeftByTerminatedDaemon()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var runtimeDirectory = Directory.CreateTempSubdirectory("pixelpointer-ipc-");
        var previousRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(runtimeDirectory.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtimeDirectory.FullName);
        using var cancellation = new CancellationTokenSource();
        Task? server = null;
        try
        {
            // O .NET remove o socket que criou ao descartá-lo; movê-lo antes simula um daemon morto.
            var boundPath = IpcChannel.SocketPath + ".orphan";
            using (var orphan = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                orphan.Bind(new UnixDomainSocketEndPoint(boundPath));
                File.Move(boundPath, IpcChannel.SocketPath);
            }

            Assert.True(File.Exists(IpcChannel.SocketPath));
            server = IpcChannel.ServeAsync(_ => { }, () => "running", cancellation.Cancel, cancellation.Token);
            Assert.Equal("running", await IpcChannel.SendControlCommandAsync("status"));
            Assert.Equal("quitting", await IpcChannel.SendControlCommandAsync("quit"));
            await server;
            Assert.False(File.Exists(IpcChannel.SocketPath));
        }
        finally
        {
            cancellation.Cancel();
            if (server is not null)
            {
                await server;
            }

            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previousRuntimeDirectory);
            runtimeDirectory.Delete(recursive: true);
        }
    }
}
