using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WindowsFormsAppSerialPort
{
    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }
    interface IReceiver
    {
        void StartReceiving(ITextChanger ipText);
        void NotifyAll();
        void Attach(IReceiverListener receiverListener);
    }

    interface IReceiverListener
    {
        void Update();
    }

    class TcpReceiver : IReceiver
    {
        List<IReceiverListener> receiverListeners;
        string ipv4Address;

        public TcpReceiver(string ipv4)
        {
            ipv4Address = ipv4;
            receiverListeners = new List<IReceiverListener>();
        }

        public void StartReceiving(ITextChanger ipText)
        {
            TcpListener server = null;
            try
            {
                // Set the TcpListener on port 13000.
                Int32 port = 13000;
                IPAddress localAddr = IPAddress.Parse(ipv4Address);

                server = new TcpListener(localAddr, port);

                // Start listening for client requests.
                server.Start();

                // Buffer for reading data
                Byte[] bytes = new Byte[256];
                String data = null;

                // Enter the listening loop.
                while (true)
                {
                    Logger.GetInstance().NotifyAll("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also use server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Logger.GetInstance().NotifyAll("Connected!");

                    data = null;

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string.
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Logger.GetInstance().NotifyAll("Received: " + data);
                        List<Message> newMessage = UtilityFunctions.MessageParser(data);
                        MessageCollection.GetInstance().AddListOfMessages(newMessage);
                        // Process the data sent by the client.
                        //data = data.ToUpper();

                        byte[] msg = System.Text.Encoding.ASCII.GetBytes(newMessage[0].Call());

                        // Send back a response.
                        stream.Write(msg, 0, msg.Length);
                        Logger.GetInstance().NotifyAll("Sent: " + data);
                    }

                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Logger.GetInstance().NotifyAll("SocketException: " + e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
                Logger.GetInstance().NotifyAll("Server has stopped listening");
            }
        }
        public void NotifyAll()
        {

        }
        public void Attach(IReceiverListener receiverListener)
        {

        }
    }

    class Receiver : IReceiver
    {
        List<IReceiverListener> receiverListeners;
        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public Receiver()
        {
            receiverListeners = new List<IReceiverListener>();
        }

        public void StartReceiving(ITextChanger ipText)
        {
            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 13000);

            ipText.changeText(ipAddress.MapToIPv4().ToString());

            // Create a TCP/IP socket.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Logger.GetInstance().NotifyAll("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read
                // more data.  
                content = state.sb.ToString();
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the
                    // client. Display it on the console.  
                    MessageCollection.GetInstance().AddListOfMessages(UtilityFunctions.MessageParser(content));
                    Logger.GetInstance().NotifyAll("Received a message");
                    // TODO: Add notif that promts new message
                    // Send response 
                    while (!MessageCollection.GetInstance().IsLastMessage())
                        Send(handler, MessageCollection.GetInstance().NextMessage().Call());
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            Logger.GetInstance().NotifyAll("Sending " + data);

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Logger.GetInstance().NotifyAll("Sent " + bytesSent + " bytes to client.");

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                Logger.GetInstance().NotifyAll(e.ToString());
            }
        }

        public void NotifyAll()
        {
            foreach (IReceiverListener receiverListener in receiverListeners)
                receiverListener.Update();
        }
        public void Attach(IReceiverListener receiverListener)
        {
            receiverListeners.Add(receiverListener);
        }
    }
}