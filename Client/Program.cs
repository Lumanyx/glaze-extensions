using System.Net.Sockets;
using System.Text;


namespace Client
{
    class Program
    {
        private const int Port = 65432;
        private const string Hostname = "127.0.0.1";
        
        /**
         * Simple CLI tool for sending text commands to the Glaze extensions daemon.
         */
        private static void Main(string[] args)
        {
            if (args.Length == 0) return;
            var command = string.Join(" ", args);
            SendCommand(command);
        }

        private static void SendCommand(string cmd)
        {
            try
            {
                using var client = new TcpClient();
                client.NoDelay = true;
                client.Connect(Hostname, Port);

                using var stream = client.GetStream();
                var data = Encoding.UTF8.GetBytes(cmd);
                stream.Write(data, 0, data.Length);
                stream.Flush();
                client.Client.Shutdown(SocketShutdown.Send);
            }
            catch (SocketException ex)
            {
                // Fails gracefully if the daemon isn't running
                Console.Error.WriteLine(ex.Message);
            }
        }
    }
}