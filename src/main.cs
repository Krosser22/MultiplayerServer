/**
*** ////////////////////////////////////////////
*** /////Autor: Juan Daniel Laserna Condado/////
*** /////Email: S6106112@live.tees.ac.uk   /////
*** /////            2016-2017             /////
*** ////////////////////////////////////////////
**/

using System;
using System.Data.SQLite;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class StateObject {
  // Client  socket.
  public Socket workSocket = null;

  // Size of receive buffer.
  public const int BufferSize = 1024;

  // Receive buffer.
  public byte[] buffer = new byte[BufferSize];

  // Received data string.
  //public StringBuilder sb = new StringBuilder();
}

public class AsynchronousSocketListener {
  // Thread signal.
  public static ManualResetEvent allDone = new ManualResetEvent(false);

  // Holds our connection with the database
  public static SQLiteConnection dbConnection;

  public static void CreateSQLiteBD() {
    Console.Write("Creating BD... ");
    // Creates an empty database file
    SQLiteConnection.CreateFile("DB.sqlite");

    // Creates a connection with our database file.
    dbConnection = new SQLiteConnection("Data Source=DB.sqlite;Version=3;");
    dbConnection.Open();

    // Creates a table
    string sql = "CREATE TABLE Users (Nick TEXT UNIQUE, Password TEXT, Email TEXT UNIQUE, PRIMARY KEY(Email));";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();

    // Inserts some values in the highscores table.
    // As you can see, there is quite some duplicate code here, we'll solve this in part two.
    sql = "INSERT INTO Users (Email, Nick, Password) VALUES ('admin@random.com', 'Admin', 'Password')";
    command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();
    sql = "INSERT INTO Users (Email, Nick, Password) VALUES ('Krosser22@random.com', 'Krosser22', 'MIAU')";
    command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();
    sql = "INSERT INTO Users (Email, Nick, Password) VALUES ('Charmander@random.com', '1', '1')";
    command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();

    Console.WriteLine("Done\n");
  }

  public static void StartSQLite() {
    if (!System.IO.File.Exists("DB.sqlite")) {
      CreateSQLiteBD();
    } else {
      // Creates a connection with our database file.
      Console.Write("Connecting to the BD... ");
      dbConnection = new SQLiteConnection("Data Source=DB.sqlite;Version=3;");
      dbConnection.Open();
      Console.WriteLine("Done\n");
    }

    // Writes the highscores to the console sorted on score in descending order.
    string sql = "SELECT * FROM Users ORDER BY Nick ASC";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    SQLiteDataReader reader = command.ExecuteReader();
    while (reader.Read()) {
      Console.WriteLine("      Email: " + reader["Email"] + "\n      Nick: " + reader["Nick"] + "\n      Password: " + reader["Password"] + "\n");
    }
    //Console.ReadLine();
  }

  private static String ProcessLogin (String nick, String password) {
    String response = String.Empty;
    string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Nick = '" + nick + "' AND Password = '" + password + "')";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    SQLiteDataReader reader = command.ExecuteReader();
    if (reader.Read()) {
      if (reader.GetBoolean(0)) {
        response = "DONE";
      } else {
        response = "ERROR";
      }
    }
    return response;
  }

  private static String ProcessCreateAccount (String email, String nick, String password) {
    String response = String.Empty;
    string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Email = '" + email + "' OR Nick = '" + nick + "')";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    SQLiteDataReader reader = command.ExecuteReader();
    if (reader.Read()) {
      if (reader.GetBoolean(0)) {
        response = "ERROR";
      } else {
        sql = "INSERT INTO Users (Email, Nick, Password) VALUES ('" + email + "', '" + nick + "', '" + password + "')";
        command = new SQLiteCommand(sql, dbConnection);
        command.ExecuteNonQuery();
        response = "DONE";
      }
    }
    return response;
  }

  private static String ProcessForgotPassword (String email) {
    String response = String.Empty;
    string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Email = '" + email + "')";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    SQLiteDataReader reader = command.ExecuteReader();
    if (reader.Read()) {
      if (reader.GetBoolean(0)) {
        response = "DONE";
      } else {
        response = "ERROR";
      }
    }
    return response;
  }

  public AsynchronousSocketListener () {}

  public static void StartListening() {
    // Data buffer for incoming data.
    byte[] bytes = new Byte[1024];

    // Establish the local endpoint for the socket.
    // The DNS name of the computer running the listener is "host.contoso.com".
    //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
    //IPAddress ipAddress = ipHostInfo.AddressList[0];
    IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
    int port = 8080;
    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);
    Console.WriteLine("{0}:{1} ONLINE\n", ipAddress, port);

    // Create a TCP/IP socket.
    Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    // Bind the socket to the local endpoint and listen for incoming connections.
    try {
      listener.Bind(localEndPoint);
      listener.Listen(100);
      
      Console.WriteLine("Server Ready\n");
      while (true) {
        // Set the event to nonsignaled state.
        allDone.Reset();

        // Start an asynchronous socket to listen for connections.
        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

        // Wait until a connection is made before continuing.
        allDone.WaitOne();
      }
    } catch (Exception e) {
      Console.WriteLine(e.ToString());
    }

    Console.WriteLine("\nPress ENTER to continue...");
    Console.Read();

    //Conect
    UdpClient client = new UdpClient(port);
    //UdpClient client = new UdpClient("clientIP", port);

    //Receive
    IPEndPoint ep = null;
    byte[] data = client.Receive(ref ep);

    //Send
    client.Send(data, data.Length);
    client.Send(data, data.Length, ep);
  }

  public static void AcceptCallback(IAsyncResult ar) {
    // Signal the main thread to continue.
    allDone.Set();

    // Get the socket that handles the client request.
    Socket listener = (Socket)ar.AsyncState;
    Socket handler = listener.EndAccept(ar);
    Console.WriteLine("New connection: [" + handler.RemoteEndPoint + "]");

    // Create the state object.
    StateObject state = new StateObject();
    state.workSocket = handler;
    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
  }

  public static void ReadCallback(IAsyncResult ar) {
    // Retrieve the state object and the handler socket from the asynchronous state object.
    StateObject state = (StateObject)ar.AsyncState;

    try {
      Socket handler = state.workSocket;

      // Read data from the client socket. 
      int bytesRead = handler.EndReceive(ar);
      if (bytesRead > 0) {
        String content = Encoding.ASCII.GetString(state.buffer, 4, bytesRead);
        content = content.Substring(0, content.IndexOf('\0'));
        Console.WriteLine("[{0}]: {1}", state.workSocket.RemoteEndPoint, content);

        // Echo the data back to the client.
        //Send(handler, content);
        ProcessNewData(handler, content);
      } else {
        //Console.WriteLine("ERROR: ReadCallback() > Read 0 bytes");
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
      }
    } catch (Exception e) {
      //Console.WriteLine(e.ToString());
      e.ToString();
      Console.WriteLine("ERROR: Lost connection with: {0}", state.workSocket.RemoteEndPoint);
    }
  }
  
  private static void ProcessNewData(Socket handler, String content) {
    String response = String.Empty;
    String[] data = content.Split(':');
    if (data.Length <= 0) {
      response = "ERROR";
      Console.WriteLine("ERROR: " + content);
    } else {
      switch (data[0]) {
        case "Login":
          if (data.Length == 3) {
            response = ProcessLogin(data[1], data[2]);
          } else {
            response = "ERROR";
            Console.WriteLine("ERROR: " + content);
          }
          break;
        case "Create":
          if (data.Length == 4) {
            response = ProcessCreateAccount(data[1], data[2], data[3]);
          } else {
            response = "ERROR";
            Console.WriteLine("ERROR: " + content);
          }
          break;
        case "Forgot":
          if (data.Length == 2) {
            response = ProcessForgotPassword(data[1]);
          } else {
            response = "ERROR";
            Console.WriteLine("ERROR: " + content);
          }
          break;
        default:
          response = "ERROR COMMAND NOT FOUND";
          break;
      }
    }
    Send(handler, response);
  }

  private static void Send (Socket handler, String data) {
    // Convert the string data to byte data using ASCII encoding.
    byte[] byteData = Encoding.ASCII.GetBytes(data);

    // Begin sending the data to the remote device.
    Console.WriteLine("[Server]: " + data);
    handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
  }

  private static void SendCallback (IAsyncResult ar) {
    try {
      // Retrieve the socket from the state object.
      Socket handler = (Socket)ar.AsyncState;

      // Complete sending the data to the remote device.
      int bytesSent = handler.EndSend(ar);
      //Console.WriteLine("Sent {0} bytes to client.\n", bytesSent);

      // Create the state object.
      StateObject state = new StateObject();
      state.workSocket = handler;
      handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
      //handler.Shutdown(SocketShutdown.Send);
      //handler.Close();
    } catch (Exception e) {
      Console.WriteLine(e.ToString());
    }
  }

  class UDPServer {
    public class UdpState {
      public IPEndPoint e;
      public UdpClient u;
    }

    public static int port = 2055;
    public static bool messageReceived = false;

    public static void ReceiveCallback (IAsyncResult ar) {
      UdpState s = (UdpState)(ar.AsyncState);
      UdpClient u = s.u;
      IPEndPoint e = s.e;

      Byte[] receiveBytes = u.EndReceive(ar, ref e);
      string receiveString = Encoding.ASCII.GetString(receiveBytes);

      Console.WriteLine("Received: {0}", receiveString);
      // The message then needs to be handled
      messageReceived = true;
    }

    public static void ReceiveMessages () {
      // Receive a message and write it to the console.
      IPEndPoint e = new IPEndPoint(IPAddress.Any, port);
      UdpClient u = new UdpClient(e);

      UdpState s = new UdpState();
      s.e = e;
      s.u = u;

      Console.WriteLine("listening for messages");
      u.BeginReceive(new AsyncCallback(ReceiveCallback), s);

      // Do some work while we wait for a message.
      while (!messageReceived) {
        // Do something
      }
    }

    public static bool messageSent = false;

    public static void SendCallback (IAsyncResult ar) {
      UdpClient u = (UdpClient)ar.AsyncState;
      Console.WriteLine("number of bytes sent: {0}", u.EndSend(ar));
      messageSent = true;
    }

    static void SendMessage (string server, string message) {
      // create the udp socket
      UdpClient u = new UdpClient();
      u.Connect(server, port);
      Byte[] sendBytes = Encoding.ASCII.GetBytes(message);
      
      // send the message the destination is defined by the call to .Connect()
      u.BeginSend(sendBytes, sendBytes.Length, new AsyncCallback(SendCallback), u);
      
      // Do some work while we wait for the send to complete. For this example, we'll just sleep
      while (!messageSent) {
        Thread.Sleep(100);
      }
    }

    public static void UPDMain () {
      UdpClient udpc = new UdpClient(port);
      Console.WriteLine("Server started, servicing on port " + port);
      IPEndPoint ep = null;
      while (true) {
        byte[] rdata = udpc.Receive(ref ep);
        string sdata = Encoding.ASCII.GetString(rdata);
        // Handle the data
        Console.WriteLine(sdata);
      }
    }
  }

  public static int Main(String[] args) {
    StartSQLite();
    new Task(UDPServer.UPDMain).Start();
    StartListening();
    return 0;
  }
}