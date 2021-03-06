using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace WindowsFormsAppClient
{
    interface IClient
    {
        void StartClient(string messageToSend, ITextChanger ipTextChanger);
    }

    // State object for receiving data from remote device.  
    public class StateObject
    {
        // Client socket.  
        public Socket WorkSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 256;
        // Receive buffer.  
        public byte[] Buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder Sb = new StringBuilder();
    }

    public class TcpAsynchromousClient : IClient
    {
        private string server;

        public TcpAsynchromousClient(string serverIP)
        {
            server = serverIP;
        }
        public void StartClient(string messageToSend, ITextChanger ipTextChanger)
        {
            try
            {
                // Create a TcpClient.
                // Note, for this client to work you need to have a TcpServer
                // connected to the same address as specified by the server, port
                // combination.
                Int32 port = 13000;
                TcpClient client = new TcpClient(server, port);

                // Translate the passed message into ASCII and store it as a Byte array.
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(messageToSend);

                // Get a client stream for reading and writing.
                //  Stream stream = client.GetStream();

                NetworkStream stream = client.GetStream();

                // Send the message to the connected TcpServer.
                stream.Write(data, 0, data.Length);

                Logger.GetInstance().WriteLog("Sent: " + messageToSend);

                // Receive the TcpServer.response.

                // Buffer to store the response bytes.
                data = new Byte[256];

                // String to store the response ASCII representation.
                String responseData = String.Empty;

                // Read the first batch of the TcpServer response bytes.
                Int32 bytes = stream.Read(data, 0, data.Length);
                responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                Logger.GetInstance().WriteLog("Received: " + responseData);

                // Close everything.
                //stream.Close();
                //client.Close();
            }
            catch (ArgumentNullException e)
            {
                Logger.GetInstance().WriteLog("ArgumentNullException: " + e);
            }
            catch (SocketException e)
            {
                Logger.GetInstance().WriteLog("SocketException: " + e);
            }
        }
    }

    public class AsynchronousClient : IClient
    {
        // The port number for the remote device.  
        private const int port = 11000;

        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        private string server;

        // The response from the remote device.  
        private static String response = String.Empty;

        public AsynchronousClient(string serverIP)
        {
            server = serverIP;
        }

        public void StartClient(string messageToSend, ITextChanger ipTextChanger)
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.   
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = IPAddress.Parse(server);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                ipTextChanger.ChangeText(ipAddress.MapToIPv4().ToString());

                // Create a TCP/IP socket.  
                Socket client = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.BeginConnect(remoteEP,
                    new AsyncCallback(connectCallback), client);
                connectDone.WaitOne();

                send(client, messageToSend + "<EOF>");
                sendDone.WaitOne();

                // Receive the response from the remote device.  
                receive(client);
                receiveDone.WaitOne();

                // Write the response to the console.  
                Logger.GetInstance().WriteLog("Response received : " + response);

                // Release the socket.  
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            catch (Exception e)
            {
                Logger.GetInstance().WriteLog(e.ToString());
            }
        }

        private static void connectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                Logger.GetInstance().WriteLog("Socket connected to " +
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.  
                connectDone.Set();
            }
            catch (Exception e)
            {
                Logger.GetInstance().WriteLog(e.ToString());
            }
        }

        private static void receive(Socket client)
        {
            try
            {
                // Create the state object.  
                StateObject state = new StateObject();
                state.WorkSocket = client;

                // Begin receiving the data from the remote device.  
                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(receiveCallback), state);
            }
            catch (Exception e)
            {
                Logger.GetInstance().WriteLog(e.ToString());
            }
        }

        private static void receiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    Console.WriteLine("still receiving");
                    // There might be more data, so store the data received so far.  
                    state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesRead));

                    // Get the rest of the data.  
                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(receiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.  
                    if (state.Sb.Length > 1)
                    {
                        response = state.Sb.ToString();
                    }
                    // Signal that all bytes have been received.  
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Logger.GetInstance().WriteLog(e.ToString());
            }
        }

        private static void send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            Logger.GetInstance().WriteLog("Whole string sent : " + data);

            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(sendCallback), client);
        }

        private static void sendCallback(IAsyncResult ar)
        {
            try
            {
                Console.WriteLine("still sending");
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                Logger.GetInstance().WriteLog("Sent "+ bytesSent + " bytes to server.");

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception e)
            {
                Logger.GetInstance().WriteLog(e.ToString());
            }
        }
    }
}