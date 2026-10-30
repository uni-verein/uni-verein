using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UniVerein.IntegrationTests.Infrastructure;

// A minimal SMTP server on a free loopback port. It never offers STARTTLS but advertises AUTH PLAIN/LOGIN, like a
// server an attacker stripped the encryption from, and records every line it receives, so tests can prove what a
// client did (or did not) send. Authentication and MAIL FROM can be made to fail.
public sealed class FakeSmtpServer : IDisposable
{
    private readonly TcpListener _listener;

    public FakeSmtpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }
    public ConcurrentQueue<string> ReceivedLines { get; } = new();
    public bool RejectAuthentication { get; set; }
    public bool RejectMailFrom { get; set; }

    public void Dispose()
    {
        _listener.Stop();
    }

    // A loopback port nothing listens on, so connecting to it is refused.
    public static int GetClosedPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (true)
            {
                TcpClient tcp = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleConnectionAsync(tcp));
            }
        }
        catch (Exception)
        {
            // Listener stopped.
        }
    }

    private async Task HandleConnectionAsync(TcpClient tcp)
    {
        try
        {
            using TcpClient client = tcp;
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII);
            await using StreamWriter writer = new(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            await writer.WriteLineAsync("220 fake ESMTP");
            while (await reader.ReadLineAsync() is { } line)
            {
                ReceivedLines.Enqueue(line);

                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-fake");
                    await writer.WriteLineAsync("250 AUTH PLAIN LOGIN");
                }
                else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                    ReceivedLines.Enqueue(await reader.ReadLineAsync() ?? string.Empty);
                    await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                    ReceivedLines.Enqueue(await reader.ReadLineAsync() ?? string.Empty);
                    await writer.WriteLineAsync(AuthenticationReply());
                }
                else if (line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync(AuthenticationReply());
                }
                else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync(RejectMailFrom ? "550 5.7.1 Sender rejected" : "250 ok");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 go ahead");
                    while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    {
                    }

                    await writer.WriteLineAsync("250 queued");
                }
                else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 ok");
                }
            }
        }
        catch (Exception)
        {
            // The client closed the connection.
        }
    }

    private string AuthenticationReply()
    {
        return RejectAuthentication ? "535 5.7.8 Authentication credentials invalid" : "235 ok";
    }
}
