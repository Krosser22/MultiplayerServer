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

  public static void StartSQLite() {
    // Creates an empty database file
    SQLiteConnection.CreateFile("DB.sqlite");

    // Creates a connection with our database file.
    dbConnection = new SQLiteConnection("Data Source=DB.sqlite;Version=3;");
    dbConnection.Open();
    
    // Creates a table
    string sql = "create table Users (Nick varchar(20), Password varchar(20))";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();

    // Inserts some values in the highscores table.
    // As you can see, there is quite some duplicate code here, we'll solve this in part two.
    sql = "insert into Users (Nick, Password) values ('Admin', 'Password')";
    command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();
    sql = "insert into Users (Nick, Password) values ('Krosser22', 'MIAU')";
    command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();
    sql = "insert into Users (Nick, Password) values ('Charmander', 'RAWR')";
    command = new SQLiteCommand(sql, dbConnection);
    command.ExecuteNonQuery();

    // Writes the highscores to the console sorted on score in descending order.
    sql = "select * from Users order by Nick asc";
    command = new SQLiteCommand(sql, dbConnection);
    SQLiteDataReader reader = command.ExecuteReader();
    /*while (reader.Read()) {
      Console.WriteLine("Nick: " + reader["Nick"] + "\nPassword: " + reader["Password"] + "\n");
    }*/
    //Console.ReadLine();
  }

  private static String ProcessLogin (String nick, String password) {
    String response = String.Empty;
    string sql = "SELECT EXISTS (SELECT * FROM Users WHERE Nick = '" + nick + "' AND Password = '" + password + "')";
    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
    SQLiteDataReader reader = command.ExecuteReader();
    if (reader.Read()) {
      if (reader.GetBoolean(0)) {
        response = "EXIST";
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
    Console.WriteLine("{0}:{1} ONLINE", ipAddress, port);

    // Create a TCP/IP socket.
    Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    // Bind the socket to the local endpoint and listen for incoming connections.
    try {
      listener.Bind(localEndPoint);
      listener.Listen(100);

      Console.WriteLine("Waiting for a connection...\n");
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
        Console.WriteLine("ERROR: ReadCallback() > Read 0 bytes");
      }
    } catch (Exception e) {
      //Console.WriteLine(e.ToString());
      e.ToString();
      Console.WriteLine("ERROR: Lost connection with: {0}", state.workSocket.RemoteEndPoint);
    }
  }
  
  private static void ProcessNewData(Socket handler, String content) {
    String response = String.Empty;
    int posDelimitator = content.IndexOf(":");
    if (posDelimitator <= 0) {
      response = "ERROR: ':'";
    } else {
      String command = content.Substring(0, posDelimitator);
      switch (command) {
        case "Login":
          String nick = content.Substring(posDelimitator + 1);
          String password = nick.Substring(nick.IndexOf(':') + 1);
          nick = nick.Substring(0, nick.IndexOf(':'));
          response = ProcessLogin(nick, password);
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

      handler.Shutdown(SocketShutdown.Send);
      handler.Close();
    } catch (Exception e) {
      Console.WriteLine(e.ToString());
    }
  }

  public static int Main(String[] args) {
    StartSQLite();
    StartListening();
    return 0;
  }
}