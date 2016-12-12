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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
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

    //////////////////////
    //////////////////////
    /*
    // Suppose the certificate is in a file...
    private static readonly string ServerCertificateFile = "server.pfx";
    private static readonly string ServerCertificatePassword = null;
    public static void TCPMain2 () {

      // later...
      int ServerPort = 9999;
      X509Certificate2 serverCertificate = new X509Certificate2(ServerCertificateFile);
      var listener = new TcpListener(IPAddress.Any, ServerPort);
      listener.Start();
      while (true) {
        using (var client = listener.AcceptTcpClient())
        using (SslStream sslStream = new SslStream(client.GetStream(), false, App_CertificateValidation)) {
          sslStream.AuthenticateAsServer(serverCertificate, true, SslProtocols.Tls12, false);
          // Send/receive from the sslStream
          // Use Read and Write
        }
      }
    }
    */
    //////////////////////
    //////////////////////

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

  public static int Main2 (string[] args) {
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

//////////////////////
//////////////////////

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

// To run this sample use the Certificate Creation Tool (Makecert.exe) to generate a test X.509 certificate and 
// place it in the local user store. 
// To generate an exchange key and make the key exportable run the following command from a Visual Studio command prompt: 

//makecert -r -pe -n "CN=CERT_SIGN_TEST_CERT" -b 01/01/2010 -e 01/01/2012 -sky exchange -ss my
namespace X509CertEncrypt {
  class Program {

    // Path variables for source, encryption, and
    // decryption folders. Must end with a backslash.
    private static string encrFolder = "./Encrypt/"; //@"C:\Encrypt\";
    private static string decrFolder = "./Decrypt/"; //@"C:\Decrypt\";
    private static string originalFile = "TestData.txt";
    private static string encryptedFile = "TestData.enc";

    static void Main (string[] args) {
      // Create an input file with test data.
      StreamWriter sw = File.CreateText(originalFile);
      sw.WriteLine("Test data to be encrypted");
      sw.Close();

      // Get the certifcate to use to encrypt the key.
      X509Certificate2 cert = GetCertificateFromStore("CN=CERT_SIGN_TEST_CERT");
      if (cert == null) {
        Console.WriteLine("Certificate 'CN=CERT_SIGN_TEST_CERT' not found.");
        Console.ReadLine();
      }
      
      // Encrypt the file using the public key from the certificate.
      EncryptFile(originalFile, (RSACryptoServiceProvider)cert.PublicKey.Key);

      // Decrypt the file using the private key from the certificate.
      DecryptFile(encryptedFile, (RSACryptoServiceProvider)cert.PrivateKey);

      //Display the original data and the decrypted data.
      Console.WriteLine("Original:   {0}", File.ReadAllText(originalFile));
      Console.WriteLine("Round Trip: {0}", File.ReadAllText(decrFolder + originalFile));
      Console.WriteLine("Press the Enter key to exit.");
      Console.ReadLine();
    }

    private static X509Certificate2 GetCertificateFromStore (string certName) {

      // Get the certificate store for the current user.
      X509Store store = new X509Store(StoreLocation.CurrentUser);
      try {
        store.Open(OpenFlags.ReadOnly);

        // Place all certificates in an X509Certificate2Collection object.
        X509Certificate2Collection certCollection = store.Certificates;
        // If using a certificate with a trusted root you do not need to FindByTimeValid, instead:
        // currentCerts.Find(X509FindType.FindBySubjectDistinguishedName, certName, true);
        X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
        X509Certificate2Collection signingCert = currentCerts.Find(X509FindType.FindBySubjectDistinguishedName, certName, false);
        if (signingCert.Count == 0)
          return null;
        // Return the first certificate in the collection, has the right name and is current.
        return signingCert[0];
      } finally {
        store.Close();
      }

    }

    // Encrypt a file using a public key.
    private static void EncryptFile (string inFile, RSACryptoServiceProvider rsaPublicKey) {
      using (AesManaged aesManaged = new AesManaged()) {
        // Create instance of AesManaged for
        // symetric encryption of the data.
        aesManaged.KeySize = 256;
        aesManaged.BlockSize = 128;
        aesManaged.Mode = CipherMode.CBC;
        using (ICryptoTransform transform = aesManaged.CreateEncryptor()) {
          RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(rsaPublicKey);
          byte[] keyEncrypted = keyFormatter.CreateKeyExchange(aesManaged.Key, aesManaged.GetType());

          // Create byte arrays to contain
          // the length values of the key and IV.
          byte[] LenK = new byte[4];
          byte[] LenIV = new byte[4];

          int lKey = keyEncrypted.Length;
          LenK = BitConverter.GetBytes(lKey);
          int lIV = aesManaged.IV.Length;
          LenIV = BitConverter.GetBytes(lIV);

          // Write the following to the FileStream
          // for the encrypted file (outFs):
          // - length of the key
          // - length of the IV
          // - ecrypted key
          // - the IV
          // - the encrypted cipher content

          int startFileName = inFile.LastIndexOf("\\") + 1;
          // Change the file's extension to ".enc"
          string outFile = encrFolder + inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) + ".enc";
          Directory.CreateDirectory(encrFolder);

          using (FileStream outFs = new FileStream(outFile, FileMode.Create)) {

            outFs.Write(LenK, 0, 4);
            outFs.Write(LenIV, 0, 4);
            outFs.Write(keyEncrypted, 0, lKey);
            outFs.Write(aesManaged.IV, 0, lIV);

            // Now write the cipher text using
            // a CryptoStream for encrypting.
            using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write)) {

              // By encrypting a chunk at
              // a time, you can save memory
              // and accommodate large files.
              int count = 0;
              int offset = 0;

              // blockSizeBytes can be any arbitrary size.
              int blockSizeBytes = aesManaged.BlockSize / 8;
              byte[] data = new byte[blockSizeBytes];
              int bytesRead = 0;

              using (FileStream inFs = new FileStream(inFile, FileMode.Open)) {
                do {
                  count = inFs.Read(data, 0, blockSizeBytes);
                  offset += count;
                  outStreamEncrypted.Write(data, 0, count);
                  bytesRead += blockSizeBytes;
                }
                while (count > 0);
                inFs.Close();
              }
              outStreamEncrypted.FlushFinalBlock();
              outStreamEncrypted.Close();
            }
            outFs.Close();
          }
        }
      }
    }
    
    // Decrypt a file using a private key.
    private static void DecryptFile (string inFile, RSACryptoServiceProvider rsaPrivateKey) {

      // Create instance of AesManaged for
      // symetric decryption of the data.
      using (AesManaged aesManaged = new AesManaged()) {
        aesManaged.KeySize = 256;
        aesManaged.BlockSize = 128;
        aesManaged.Mode = CipherMode.CBC;

        // Create byte arrays to get the length of
        // the encrypted key and IV.
        // These values were stored as 4 bytes each
        // at the beginning of the encrypted package.
        byte[] LenK = new byte[4];
        byte[] LenIV = new byte[4];

        // Consruct the file name for the decrypted file.
        string outFile = decrFolder + inFile.Substring(0, inFile.LastIndexOf(".")) + ".txt";

        // Use FileStream objects to read the encrypted
        // file (inFs) and save the decrypted file (outFs).
        using (FileStream inFs = new FileStream(encrFolder + inFile, FileMode.Open)) {

          inFs.Seek(0, SeekOrigin.Begin);
          inFs.Seek(0, SeekOrigin.Begin);
          inFs.Read(LenK, 0, 3);
          inFs.Seek(4, SeekOrigin.Begin);
          inFs.Read(LenIV, 0, 3);

          // Convert the lengths to integer values.
          int lenK = BitConverter.ToInt32(LenK, 0);
          int lenIV = BitConverter.ToInt32(LenIV, 0);

          // Determine the start postition of
          // the ciphter text (startC)
          // and its length(lenC).
          int startC = lenK + lenIV + 8;
          int lenC = (int)inFs.Length - startC;

          // Create the byte arrays for
          // the encrypted AesManaged key,
          // the IV, and the cipher text.
          byte[] KeyEncrypted = new byte[lenK];
          byte[] IV = new byte[lenIV];

          // Extract the key and IV
          // starting from index 8
          // after the length values.
          inFs.Seek(8, SeekOrigin.Begin);
          inFs.Read(KeyEncrypted, 0, lenK);
          inFs.Seek(8 + lenK, SeekOrigin.Begin);
          inFs.Read(IV, 0, lenIV);
          Directory.CreateDirectory(decrFolder);
          // Use RSACryptoServiceProvider
          // to decrypt the AesManaged key.
          byte[] KeyDecrypted = rsaPrivateKey.Decrypt(KeyEncrypted, false);

          // Decrypt the key.
          using (ICryptoTransform transform = aesManaged.CreateDecryptor(KeyDecrypted, IV)) {

            // Decrypt the cipher text from
            // from the FileSteam of the encrypted
            // file (inFs) into the FileStream
            // for the decrypted file (outFs).
            using (FileStream outFs = new FileStream(outFile, FileMode.Create)) {

              int count = 0;
              int offset = 0;

              int blockSizeBytes = aesManaged.BlockSize / 8;
              byte[] data = new byte[blockSizeBytes];

              // By decrypting a chunk a time,
              // you can save memory and
              // accommodate large files.

              // Start at the beginning
              // of the cipher text.
              inFs.Seek(startC, SeekOrigin.Begin);
              using (CryptoStream outStreamDecrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write)) {
                do {
                  count = inFs.Read(data, 0, blockSizeBytes);
                  offset += count;
                  outStreamDecrypted.Write(data, 0, count);

                }
                while (count > 0);

                outStreamDecrypted.FlushFinalBlock();
                outStreamDecrypted.Close();
              }
              outFs.Close();
            }
            inFs.Close();
          }

        }

      }
    }
  }

  class CertInfo {
    //Reads a file.
    internal static byte[] ReadFile (string fileName) {
      FileStream f = new FileStream(fileName, FileMode.Open, FileAccess.Read);
      int size = (int)f.Length;
      byte[] data = new byte[size];
      size = f.Read(data, 0, size);
      f.Close();
      return data;
    }
    //Main method begins here.
    static void Main (string[] args) {
      //Test for correct number of arguments.
      if (args.Length < 1) {
        Console.WriteLine("Usage: CertInfo <filename>");
        return;
      }
      try {
        X509Certificate2 x509 = new X509Certificate2();
        //Create X509Certificate2 object from .cer file.
        byte[] rawData = ReadFile(args[0]);

        x509.Import(rawData);

        //Print to console information contained in the certificate.
        Console.WriteLine("{0}Subject: {1}{0}", Environment.NewLine, x509.Subject);
        Console.WriteLine("{0}Issuer: {1}{0}", Environment.NewLine, x509.Issuer);
        Console.WriteLine("{0}Version: {1}{0}", Environment.NewLine, x509.Version);
        Console.WriteLine("{0}Valid Date: {1}{0}", Environment.NewLine, x509.NotBefore);
        Console.WriteLine("{0}Expiry Date: {1}{0}", Environment.NewLine, x509.NotAfter);
        Console.WriteLine("{0}Thumbprint: {1}{0}", Environment.NewLine, x509.Thumbprint);
        Console.WriteLine("{0}Serial Number: {1}{0}", Environment.NewLine, x509.SerialNumber);
        Console.WriteLine("{0}Friendly Name: {1}{0}", Environment.NewLine, x509.PublicKey.Oid.FriendlyName);
        Console.WriteLine("{0}Public Key Format: {1}{0}", Environment.NewLine, x509.PublicKey.EncodedKeyValue.Format(true));
        Console.WriteLine("{0}Raw Data Length: {1}{0}", Environment.NewLine, x509.RawData.Length);
        Console.WriteLine("{0}Certificate to string: {1}{0}", Environment.NewLine, x509.ToString(true));

        Console.WriteLine("{0}Certificate to XML String: {1}{0}", Environment.NewLine, x509.PublicKey.Key.ToXmlString(false));

        //Add the certificate to a X509Store.
        X509Store store = new X509Store();
        store.Open(OpenFlags.MaxAllowed);
        store.Add(x509);
        store.Close();
      } catch (DirectoryNotFoundException) {
        Console.WriteLine("Error: The directory specified could not be found.");
      } catch (IOException) {
        Console.WriteLine("Error: A file in the directory could not be accessed.");
      } catch (NullReferenceException) {
        Console.WriteLine("File must be a .cer file. Program does not have access to that type of file.");
      }
    }
  }
}