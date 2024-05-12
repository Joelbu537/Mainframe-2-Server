using Mainframe_2_Server;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace TcpListenerExample
{
    class Program
    {
        static int port = 33533;
        static string version = "2.0.1";
        static async Task Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 33533);
            listener.Start();
            Console.WriteLine($"Server listening on port {port}...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Incoming connection...");
                try
                {
                    Task.Run(() => HandleClient(client));
                }catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync("Connection could not be established!");
                    Console.WriteLine(ex.ToString());
                }


            }
        }

        static void HandleClient(TcpClient client)
        {
            using (NetworkStream stream = client.GetStream())
            {
                Console.WriteLine($"Connected to {client.Client.RemoteEndPoint}!");
                byte[] buffer = new byte[1024];
                int bytesRead;
                try
                {
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        string dataReceived = Encoding.Unicode.GetString(buffer, 0, bytesRead);
                        Console.WriteLine("Received: " + dataReceived);
                        string[] content = dataReceived.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                        if (content[0] == version)
                        {
                            switch (content[1])
                            {
                                case "verify":
                                    string username = content[3];
                                    string hash = content[4];
                                    bool exists = false;
                                    bool pwd_correct = false;
                                    using (MainframeEntities context = new MainframeEntities())
                                    {
                                        var query = context.Users.SqlQuery("SELECT * FROM Users WHERE username = @username", new SqlParameter("@username", username));


                                        foreach (Users u in query)
                                        {
                                            exists = true;
                                        }
                                        query = context.Users.SqlQuery("SELECT * FROM Users WHERE pwd_hash = @hash AND username = @username", new SqlParameter("@hash", hash), new SqlParameter("@username", username));

                                        foreach (Users u in query)
                                        {
                                            pwd_correct = true;
                                        }
                                    }
                                    if (exists)
                                    {
                                        string response = "";
                                        byte[] responseData;
                                        if (pwd_correct)
                                        {
                                            using (MainframeEntities context = new MainframeEntities())
                                            {
                                                int owner_id = 0;
                                                var query2 = context.Users.SqlQuery("SELECT * FROM Users WHERE username = @username", new SqlParameter("@username", username.ToLower()));
                                                foreach(Users u in query2)
                                                {
                                                    owner_id = u.id;
                                                }
                                                Logins l = new Logins();
                                                l.status = true;
                                                l.owner_id = owner_id;
                                                l.client_ip = client.Client.RemoteEndPoint.ToString();
                                                l.occured = DateTime.Now;
                                                context.Logins.Add(l);
                                                context.SaveChanges();
                                            }
                                            response = $"{version}\r\nverify\r\n2";
                                            responseData = Encoding.Unicode.GetBytes(response);
                                            stream.Write(responseData, 0, responseData.Length);
                                        }
                                        else
                                        {
                                            using (MainframeEntities context = new MainframeEntities())
                                            {
                                                int owner_id = 0;
                                                var query2 = context.Users.SqlQuery("SELECT * FROM Users WHERE username = @username", new SqlParameter("@username", username.ToLower()));
                                                foreach (Users u in query2)
                                                {
                                                    owner_id = u.id;
                                                }
                                                Logins l = new Logins();
                                                l.status = false;
                                                l.owner_id = owner_id;
                                                l.client_ip = client.Client.RemoteEndPoint.ToString();
                                                l.occured = DateTime.Now;
                                                context.Logins.Add(l);
                                                context.SaveChanges();
                                            }
                                            response = $"{version}\r\nverify\r\n1";
                                            responseData = Encoding.Unicode.GetBytes(response);
                                            stream.Write(responseData, 0, responseData.Length);
                                        }
                                    }
                                    else
                                    {
                                        string response = $"{version}\r\nverify\r\n0";
                                        byte[] responseData = Encoding.Unicode.GetBytes(response);
                                        stream.Write(responseData, 0, responseData.Length);
                                    }
                                    break;
                                case "register":
                                    using (MainframeEntities context = new MainframeEntities())
                                    {
                                        username = content[3];
                                        hash = content[4];
                                        exists = false;
                                        var query = context.Users.SqlQuery("SELECT * FROM Users WHERE username = @username", new SqlParameter("@username", username));


                                        foreach (Users u in query)
                                        {
                                            exists = true;
                                        }
                                        if (!exists)
                                        {
                                            Users u = new Users();
                                            u.username = username;
                                            u.pwd_hash = hash;
                                            context.Users.Add(u);
                                            context.SaveChanges();
                                            string response = $"{version}\r\nregister\r\n1";
                                            byte[] responseData = Encoding.Unicode.GetBytes(response);
                                            stream.Write(responseData, 0, responseData.Length);
                                        }
                                        else
                                        {
                                            string response = $"{version}\r\nregister\r\n2";
                                            byte[] responseData = Encoding.Unicode.GetBytes(response);
                                            stream.Write(responseData, 0, responseData.Length);
                                        }
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            string response = $"{version}\r\noutdated\r\noutdated";
                            byte[] responseData = Encoding.Unicode.GetBytes(response);
                            stream.Write(responseData, 0, responseData.Length);
                            break;
                        }
                        stream.Flush();
                    }
                    client.Close();
                    stream.Close();
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            client.Close();
            Console.WriteLine("Client disconnected!");
        }
    }
}
