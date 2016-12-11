/**
*** ////////////////////////////////////////////
*** /////Autor: Juan Daniel Laserna Condado/////
*** /////Email: S6106112@live.tees.ac.uk   /////
*** /////            2016-2017             /////
*** ////////////////////////////////////////////
**/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class Server {
  //Size of receive buffer.
  public const int kBufferSize = 1024;

  //The list of connected users
  public static ConcurrentDictionary<int, StateObject> connectedList = new ConcurrentDictionary<int, StateObject>();

  public class StateObject {
    //Receive buffer.
    public byte[] buffer = new byte[kBufferSize];

    //Client socket.
    public Socket workSocket = null;
  }

  public class DB {
    //Holds our connection with the database
    public static SQLiteConnection Connection;

    public static void CreateDB () {
      //Creates an empty database file
      SQLiteConnection.CreateFile("DB.sqlite");
      Console.WriteLine("[Created new BD]");

      //Creates a connection with our database file.
      Connection = new SQLiteConnection("Data Source=DB.sqlite;Version=3;");
      Connection.Open();
      Console.WriteLine("[Connected to the BD]");

      //Creates the Users table
      string sql = "CREATE TABLE Users (Nick TEXT UNIQUE, Password TEXT, Email TEXT UNIQUE, PRIMARY KEY(Email));";
      SQLiteCommand command = new SQLiteCommand(sql, Connection);
      command.ExecuteNonQuery();

      /*sql = "INSERT INTO Users (Email, Nick, Password) VALUES ('Krosser22@random.com', 'Krosser22', 'a123456*')";
      command = new SQLiteCommand(sql, dbConnection);
      command.ExecuteNonQuery();*/
    }

    public static void DBMain () {
      if (!System.IO.File.Exists("DB.sqlite")) {
        //Creates and connects with the DB
        CreateDB();
      } else {
        //Connects to the DB
        Connection = new SQLiteConnection("Data Source=DB.sqlite;Version=3;");
        Connection.Open();
        Console.WriteLine("[Connected to the BD]");
      }
      
      string sql = "SELECT * FROM Users ORDER BY Nick ASC";
      SQLiteCommand command = new SQLiteCommand(sql, Connection);
      SQLiteDataReader reader = command.ExecuteReader();
      while (reader.Read()) {
        Console.WriteLine("-Email: " + reader["Email"]);
        Console.WriteLine("-Nick: " + reader["Nick"]);
        Console.WriteLine("-Password: " + reader["Password"]);
        Console.WriteLine();
      }
    }
  }
  
  public class TCPServer {
    //Thread signal.
    private static ManualResetEvent allDone = new ManualResetEvent(false);

    //The next ID of the connected users
    private static int nextID = 0;

    private static string ProcessLogin (string nick, string password, Socket socket) {
      string response = "Login:ERROR\n";
      //string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Nick = '" + nick + "' AND Password = '" + password + "')";
      string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Nick = @nick AND Password = @password)";
      SQLiteCommand command = new SQLiteCommand(sql, DB.Connection);
      command.Parameters.AddWithValue("@nick", nick);
      command.Parameters.AddWithValue("@password", password);
      SQLiteDataReader reader = command.ExecuteReader();
      if (reader.Read()) {
        if (reader.GetBoolean(0)) {
          string ID = "";
          Parallel.ForEach(connectedList, keyValuePair => {
            if (keyValuePair.Value.workSocket == socket) {
              ID = keyValuePair.Key.ToString();
            }
          });
          response = "Login:" + ID + "\n";
          Task.Run(() => ConnectNewPlayer(socket, ID));
        }
      }
      return response;
    }

    private static string ProcessCreateAccount (string email, string nick, string password) {
      string response = "Create:ERROR\n";
      //string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Email = '" + email + "' OR Nick = '" + nick + "')";
      string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Email = @email OR Nick = @nick)";
      SQLiteCommand command = new SQLiteCommand(sql, DB.Connection);
      command.Parameters.AddWithValue("@email", email);
      command.Parameters.AddWithValue("@nick", nick);
      SQLiteDataReader reader = command.ExecuteReader();
      if (reader.Read()) {
        if (!reader.GetBoolean(0)) {
          //sql = "INSERT INTO Users (Email, Nick, Password) VALUES ('" + email + "', '" + nick + "', '" + password + "')";
          sql = "INSERT INTO Users (Email, Nick, Password) VALUES (@email, @nick, @password)";
          command = new SQLiteCommand(sql, DB.Connection);
          command.Parameters.AddWithValue("@email", email);
          command.Parameters.AddWithValue("@nick", nick);
          command.Parameters.AddWithValue("@password", password);
          command.ExecuteNonQuery();
          response = "Create:Done\n";
        }
      }
      return response;
    }

    private static string ProcessForgotPassword (string email) {
      string response = "Forgot:ERROR\n";
      //string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Email = '" + email + "')";
      string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Email = @email)";
      SQLiteCommand command = new SQLiteCommand(sql, DB.Connection);
      command.Parameters.AddWithValue("@email", email);
      SQLiteDataReader reader = command.ExecuteReader();
      if (reader.Read()) {
        if (reader.GetBoolean(0)) {
          response += "Forgot:Done\n";
        }
      }
      return response;
    }

    private static void ProcessTCPMsg(Socket handler, string content) {
      string response = "";
      string[] data = content.Split(':');
      if (data.Length <= 0) {
        response = "ERROR:BAD FORMAT";
        Console.WriteLine("ERROR: BAD FORMAT: [{0}]", content);
      } else {
        switch (data[0]) {
          case "Login":
            if (data.Length == 3) {
              response = ProcessLogin(data[1], data[2], handler);
            } else {
              response = "ERROR";
              Console.WriteLine("ERROR: Login: [{0}]", content);
            }
            break;
          case "Create":
            if (data.Length == 4) {
              response = ProcessCreateAccount(data[1], data[2], data[3]);
            } else {
              response = "ERROR";
              Console.WriteLine("ERROR: Create: [{0}]", content);
            }
            break;
          case "Forgot":
            if (data.Length == 2) {
              response = ProcessForgotPassword(data[1]);
            } else {
              response = "ERROR";
              Console.WriteLine("ERROR: Forgot: [{0}]", content);
            }
            break;
          default:
            response = "ERROR COMMAND NOT FOUND";
            Console.WriteLine("ERROR: Command not found: [{0}]", content);
            break;
        }
      }
      Send(handler, response);
    }

    private static void ConnectNewPlayer (Socket socket, string newPlayerID) {
      string response = "";
      Parallel.ForEach(connectedList, keyValuePair => {
        if (keyValuePair.Value.workSocket != socket) {
          //Send the info of the new player to all the connected players
          response = "AddPlayer:" + newPlayerID + "\n";
          Send(keyValuePair.Value.workSocket, response);

          //Send the info of the players connected to the new player
          response = "AddPlayer:" + keyValuePair.Key + "\n";
          Send(socket, response);
        }
      });
    }

    private static void DisconnectPlayer (Socket socket) {
      int key = 0;
      StateObject state;
      Parallel.ForEach(connectedList, keyValuePair => {
        if (keyValuePair.Value.workSocket == socket) {
          key = keyValuePair.Key;
          state = keyValuePair.Value;
        }
      });
      connectedList.TryRemove(key, out state);
    }

    private static void SendCallback (IAsyncResult ar) {
      //Create the state object.
      StateObject state = new StateObject();

      //Retrieve the socket from the state object.
      Socket handler = (Socket)ar.AsyncState;

      try {
        int bytesSent = handler.EndSend(ar);
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, kBufferSize, 0, new AsyncCallback(ReadCallback), state);
      } catch (Exception e) {
        //Console.WriteLine(e.ToString());
        e.ToString();
        Console.WriteLine("ERROR1: Lost connection with: {0}", state.workSocket.RemoteEndPoint.ToString());
        DisconnectPlayer(handler);
      }
    }

    private static void Send (Socket handler, string data) {
      //Convert the string data to byte data using ASCII encoding.
      byte[] byteData = Encoding.ASCII.GetBytes(data);

      //Begin sending the data to the remote device.
      Console.WriteLine("[Server]: {0}", data);
      Thread.Sleep(100);
      handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
    }

    private static void ReadCallback (IAsyncResult ar) {
      //Retrieve the state object and the handler socket from the asynchronous state object.
      StateObject state = (StateObject)ar.AsyncState;

      //Retrieve the socket from the state object.
      Socket handler = state.workSocket;

      try {
        int bytesRead = handler.EndReceive(ar);
        if (bytesRead > 0) {
          //Get and clean the msg
          string msg = Encoding.ASCII.GetString(state.buffer, 4, bytesRead);
          msg = msg.Substring(0, msg.IndexOf('\0'));

          Console.WriteLine("[{0}]: {1}", state.workSocket.RemoteEndPoint.ToString(), msg);
          ProcessTCPMsg(handler, msg);
        }
      } catch (Exception e) {
        //Console.WriteLine(e.ToString());
        e.ToString();
        Console.WriteLine("ERROR2: Lost connection with: {0}", state.workSocket.RemoteEndPoint.ToString());
        DisconnectPlayer(handler);
      }
    }

    private static void AcceptCallback (IAsyncResult ar) {
      //Signal the main thread to continue.
      allDone.Set();

      //Get the socket that handles the client request.
      Socket listener = (Socket)ar.AsyncState;
      Socket handler = listener.EndAccept(ar);
      Console.WriteLine("[New connection: {0}]", handler.RemoteEndPoint);

      //Create the state object.
      StateObject state = new StateObject();
      connectedList.TryAdd(++nextID, state);

      state.workSocket = handler;
      handler.BeginReceive(state.buffer, 0, kBufferSize, 0, new AsyncCallback(ReadCallback), state);
    }

    public static void TCPMain () {
      //Data buffer for incoming data.
      byte[] bytes = new Byte[kBufferSize];

      //Establish the local endpoint for the socket.
      //The DNS name of the computer running the listener is "host.contoso.com".
      //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
      //IPAddress ipAddress = ipHostInfo.AddressList[0];
      IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
      int port = 8080;
      IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

      //Create a TCP/IP socket.
      Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

      //Bind the socket to the local endpoint and listen for incoming connections.
      try {
        listener.Bind(localEndPoint);
        listener.Listen(100);
        
        Console.WriteLine("[TCP Server ready: {0}:{1}]", ipAddress, port);
        while (true) {
          //Set the event to nonsignaled state.
          allDone.Reset();

          //Start an asynchronous socket to listen for connections.
          listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

          //Wait until a connection is made before continuing.
          allDone.WaitOne();
        }

        //listener.Shutdown(SocketShutdown.Send);
        //listener.Close();
      } catch (Exception e) {
        Console.WriteLine(e.ToString());
      }
    }
  }

  public class UDPServer {
    public class UdpState {
      public IPEndPoint e;
      public UdpClient u;
    }

    public static bool messageReceived = false;

    public static bool messageSent = false;

    public static ConcurrentDictionary<string, IPEndPoint> UDPList = new ConcurrentDictionary<string, IPEndPoint>();
    
    public static void SendCallback (IAsyncResult ar) {
      UdpClient u = (UdpClient)ar.AsyncState;
      u.EndSend(ar);
      messageSent = true;
    }

    static void SendMessage (string server, string message) {
      //Create the udp socket
      UdpClient u = new UdpClient();
      string ip = server.Substring(0, server.IndexOf(":"));
      int port = int.Parse(server.Substring(server.IndexOf(":") + 1));
      u.Connect(ip, port);
      Byte[] sendBytes = Encoding.ASCII.GetBytes(message);

      //Send the message the destination is defined by the call to .Connect()
      u.BeginSend(sendBytes, sendBytes.Length, new AsyncCallback(SendCallback), u);

      //Do some work while we wait for the send to complete. For this example, we'll just sleep
      while (!messageSent) {
        Thread.Sleep(100);
      }
    }

    public static void ProcessUDPMsg (string data) {
      Parallel.ForEach(UDPList, keyValuePair => {
        SendMessage(keyValuePair.Value.ToString(), data);
      });
    }

    public static void UPDMain () {
      int Port = 2055;
      IPEndPoint ep = null;
      UdpClient udpc = new UdpClient(Port);
      Console.WriteLine("[UDP Server ready: {0}]", Port);

      while (true) {
        byte[] rdata = udpc.Receive(ref ep);
        string sdata = Encoding.ASCII.GetString(rdata);
        UDPList.TryAdd(ep.ToString(), ep);

        //Handle the data
        //Console.WriteLine("New UPD msg: {0}", sdata);
        ProcessUDPMsg(sdata);
      }
    }

    public static void UPDMain2 () {
      int Port = 2055;
      IPEndPoint ep = null;
      UdpClient udpc = new UdpClient(Port);
      Console.WriteLine("[UDP Server ready: {0}]", Port);

      Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      IPAddress broadcast = IPAddress.Parse("127.0.0.255");
      s.EnableBroadcast = true;
      udpc.EnableBroadcast = true;
      IPEndPoint epBroadcast = new IPEndPoint(broadcast, Port);
      while (true) {
        byte[] rdata = udpc.Receive(ref ep);
        string sdata = Encoding.ASCII.GetString(rdata);
        s.SendTo(rdata, epBroadcast);
        Console.WriteLine("Message sent to the broadcast address");
      }
    }

    public static void UPDMain3 () {
      /*Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      s.EnableBroadcast = true;
      //IPAddress broadcast = IPAddress.Parse("100.76.205.255");
      //IPAddress broadcast = IPAddress.Parse("127.255.255.255");
      IPAddress broadcast = IPAddress.Broadcast;
      byte[] sendbuf = Encoding.ASCII.GetBytes("HOLA");
      IPEndPoint ep = new IPEndPoint(broadcast, 2055);
      s.SendTo(sendbuf, ep);
      Console.WriteLine("Message sent to the broadcast address");*/

      //////////////////////

      UdpClient client = new UdpClient();
      client.EnableBroadcast = true;
      //IPEndPoint ip = new IPEndPoint(IPAddress.Parse("100.76.205.255"), 15000);
      IPEndPoint ip = new IPEndPoint(IPAddress.Parse("127.255.255.255"), 15000);
      //IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, 15000);
      byte[] bytes = Encoding.ASCII.GetBytes("HOLA");
      client.Send(bytes, bytes.Length, ip);
    }
  }

  public static int Main (string[] args) {
    DB.DBMain();
    new Task(TCPServer.TCPMain).Start();
    new Task(UDPServer.UPDMain).Start();

    string command = "";
    while (command != "exit") {
      command = Console.ReadLine();
      //if (command != "") Console.WriteLine("Command: {0}", command);
    }

    return 0;
  }
}

//Check if a player that connects is already connected (and then dont create a new player, just use the old one and give him all the data of the game)
//Security: Avoid brute force --> Use minimum time to check the same user with the same IP:PORT beetwen one and another check
//SSL/TLS --> To avoid man in the middle

/*public class sslTesting {
  // Suppose the certificate is in a file...
  private static readonly string ServerCertificateFile = "server.pfx";
  private static readonly string ServerCertificatePassword = null;

  // later...
  X509Certificate2 serverCertificate = new X509Certificate2(ServerCertificateFile);
  TcpListener listener = new TcpListener(IPAddress.Any, ServerPort);
  listener.Start();
  while (true) {
    using (TcpListener client = listener.AcceptTcpClient())
    using (SslStream sslStream = new SslStream(client.GetStream(), false, App_CertificateValidation)) {
      sslStream.AuthenticateAsServer(serverCertificate, true, SslProtocols.Tls12, false);
      // Send/receive from the sslStream
      // Use Read and Write
    }
  }


  //Client
  X509Certificate2 clientCertificate = new X509Certificate2 (ClientCertificateFile);
  X509CertificateCollection clientCertificateCollection = new X509CertificateCollection(new X509Certificate[] {
    clientCertificate
  });
  using (TcpClient client = new TcpClient(ServerHostName, ServerPort))
  using (SslStream sslStream = new SslStream(client.GetStream(), false, App_CertificateValidation)) {
    sslStream.AuthenticateAsClient(ServerCertificateName, clientCertificateCollection, SslProtocols.Tls12, false);
    // Send/receive from the sslStream
    // Use Read and Write
  }

  
  //Certificate errors
  bool App_CertificateValidation (Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
    if (sslPolicyErrors == SslPolicyErrors.None) {
      return true;
    }
    if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors) {
      return true;
    } //we don 't have a proper certificate tree
    return false;
  }
}*/