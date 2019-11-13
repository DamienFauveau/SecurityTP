﻿using System;
using System.Net;
using System.IO;
using System.Web;
using System.Data.SQLite;
using System.Text;
using System.Security.Cryptography;

namespace Security
{
    class Program
    {
        static void Main(string[] args)
        {
        	SQLiteConnection sqlite_conn;
         	sqlite_conn = CreateConnection();
         	//DeleteTable(sqlite_conn);
    	    //CreateTable(sqlite_conn);
        	//InsertData(sqlite_conn);
        	//ReadData(sqlite_conn);
            SimpleListenerExample(new string[]{"http://localhost:8080/"}, sqlite_conn);
            sqlite_conn.Close();
        }

        static SQLiteConnection CreateConnection()
	    {
	        SQLiteConnection sqlite_conn;
	        sqlite_conn = new SQLiteConnection("Data Source=database.db;Version=3;New=True;Compress=True;");
	        try 
	        {
	           sqlite_conn.Open();
	        }
	        catch (Exception ex)
	        {
				throw new Exception(ex.ToString());
	        }
	        return sqlite_conn;
	    }

	    //////////// DB QUERIES ////////////
	    static void DeleteTable(SQLiteConnection conn)
      	{
	        SQLiteCommand sqlite_cmd;
	        
	        string Createsql = "DROP TABLE IF EXISTS Users";
	        sqlite_cmd = conn.CreateCommand();
	        
	        sqlite_cmd.CommandText = Createsql;
	        sqlite_cmd.ExecuteNonQuery();
      	}

	    static void CreateTable(SQLiteConnection conn)
      	{
	        SQLiteCommand sqlite_cmd;
	        
	        string Createsql = "CREATE TABLE Users(Username VARCHAR(50), Password VARCHAR(255))";
	        sqlite_cmd = conn.CreateCommand();
	        
	        sqlite_cmd.CommandText = Createsql;
	        sqlite_cmd.ExecuteNonQuery();
      	}

      	static void InsertData(SQLiteConnection conn)
      	{
         	SQLiteCommand sqlite_cmd;
         	sqlite_cmd = conn.CreateCommand();
         	
         	sqlite_cmd.CommandText = "INSERT INTO Users(Username, Password) VALUES ('username','"+GenerateSHA512String("password")+"');";
         	sqlite_cmd.ExecuteNonQuery();
         	
         	sqlite_cmd.CommandText = "INSERT INTO Users(Username, Password) VALUES ('admin','"+GenerateSHA512String("admin")+"');";
         	sqlite_cmd.ExecuteNonQuery();
      	}

      	static void ReadData(SQLiteConnection conn)
      	{
         	SQLiteDataReader sqlite_datareader;
         	SQLiteCommand sqlite_cmd;
         	
         	sqlite_cmd = conn.CreateCommand();
         	sqlite_cmd.CommandText = "SELECT * FROM Users";
 		
         	sqlite_datareader = sqlite_cmd.ExecuteReader();
         	while (sqlite_datareader.Read())
         	{
            	Console.WriteLine(sqlite_datareader["username"].ToString());
            	Console.WriteLine(sqlite_datareader["password"].ToString() + "\n");
         	}
      	}
      	///////////////////////////////////

      	////////////// HTTP ///////////////
		static void SimpleListenerExample(string[] prefixes, SQLiteConnection sqlite_conn)
		{
		    if (!HttpListener.IsSupported)
		    {
		        Console.WriteLine ("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
		        return;
		    }
		 
		    if (prefixes == null || prefixes.Length == 0)
		      throw new ArgumentException("prefixes");
		    
		    HttpListener listener = new HttpListener();
		    foreach (string s in prefixes)
		    {
		        listener.Prefixes.Add(s);
		    }
			
			listener.Start();
			Console.WriteLine("Server running");
			while(listener.IsListening) 
			{
				HttpListenerContext context = listener.GetContext();
			    HttpListenerRequest request = context.Request;
			    HttpListenerResponse response = context.Response;
				
				String url = request.Url.AbsolutePath;
				Console.WriteLine(url);

				if(url == "/login")
				{
					string[] postCredentials = LoginRequestData(request);
				    if(postCredentials.Length > 0)
				    {
				    	Console.WriteLine(postCredentials[0]);
				    	Console.WriteLine(postCredentials[1]);
				    	if(ConnectUser(sqlite_conn, postCredentials[0], postCredentials[1]))
				    	{
				    		Console.WriteLine("Credentials OK");
				    		SetSession();
				    		response.Redirect("http://localhost:8080/upload");
				    	}
				    	else
				    	{
				    		Console.WriteLine("Wrong username or password");
				    	}
			    	}
			    }
			    else if(url == "/upload")
			    {
			    	if(!CheckSession())
			    	{
			    		//response.Redirect("http://localhost:8080/login");
			    	}
			    	String fileUpload = UploadRequestData(request);
			    	Console.WriteLine(fileUpload);
			    }

				RenderHtml(url, response);
			}
		}

		static void RenderHtml(String url, HttpListenerResponse response)
		{
			String responseString;
			switch(url)
			{
				case "/login":
				responseString = @"
				    <html>
				    	<body>
				    		<form method='post'>
				    			<input name='username' type='text' placeholder='username'><br>
				    			<input name='password' type='password' placeholder='password'><br>
				    			<input type='submit'>
				    		</form>
				    	</body>
				    </html>
				";
				break;

				case "/upload":
				responseString = @"
				    <html>
				    	<body>
				    		Connected as 
				    		<form method='post'>
				    			<input name='fileUpload' type='file' placeholder='File to upload'><br>
				    			<input type='submit'>
				    		</form>
				    	</body>
				    </html>
				";
				break;

				default:
				responseString = @"
				    <html>
				    	<body>
				    		404 Error
				    	</body>
				    </html>
				";
				break;
			}

			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

		    response.ContentLength64 = buffer.Length;
		    System.IO.Stream output = response.OutputStream;
		    output.Write(buffer,0,buffer.Length);

		    output.Close();
		}
		///////////////////////////////////

		////////////// LOGIN //////////////
		static string[] LoginRequestData(HttpListenerRequest request)
		{
		    if (!request.HasEntityBody)
		    {
		        Console.WriteLine("No client data was sent with the request.");
		        return new string[0];
		    }
		    System.IO.Stream body = request.InputStream;
		    System.Text.Encoding encoding = request.ContentEncoding;
		    System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

		    string s = reader.ReadToEnd();
		    
		    string[] posts = s.Split("&");
		    string username = posts[0].Split("=")[1];
		    string password = posts[1].Split("=")[1];
		    
		    body.Close();
		    reader.Close();
		    
		    return new string[]{username, password};
		}

		static bool ConnectUser(SQLiteConnection conn, string username, string password)
      	{
         	SQLiteDataReader sqlite_datareader;
         	SQLiteCommand sqlite_cmd;
         	
         	sqlite_cmd = conn.CreateCommand();
         	sqlite_cmd.CommandText = "SELECT * FROM Users WHERE username LIKE '" + HttpUtility.UrlDecode(username) + "' AND password LIKE '" + HttpUtility.UrlDecode(password) + "';";
 		
         	sqlite_datareader = sqlite_cmd.ExecuteReader();
         	while (sqlite_datareader.Read())
         	{
            	return true;
         	}
         	
         	return false;
      	}

      	static bool SetSession() // TODO
      	{
      		return false;
      	}
      	///////////////////////////////////

      	////////////// UPLOAD /////////////
		static String UploadRequestData(HttpListenerRequest request)
		{
		    if (!request.HasEntityBody)
		    {
		        Console.WriteLine("No client data was sent with the request.");
		        return null;
		    }
		    System.IO.Stream body = request.InputStream;
		    System.Text.Encoding encoding = request.ContentEncoding;
		    System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

		    string s = reader.ReadToEnd();
		    string[] posts = s.Split("&");
		    string fileUpload = posts[0].Split("=")[1];

		    body.Close();
		    reader.Close();
		    
		    return new String(fileUpload);
		}

      	static bool CheckSession() // TODO
      	{
      		return false;
      	}
      	///////////////////////////////////

      	//////////////// HASH /////////////
      	public static string GenerateSHA256String(string inputString)
        {
            SHA256 sha256 = SHA256Managed.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(inputString);
            byte[] hash = sha256.ComputeHash(bytes);
            return GetStringFromHash(hash);
        }

        public static string GenerateSHA512String(string inputString)
        {
            SHA512 sha512 = SHA512Managed.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(inputString);
            byte[] hash = sha512.ComputeHash(bytes);
            return GetStringFromHash(hash);
        }

        private static string GetStringFromHash(byte[] hash)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                result.Append(hash[i].ToString("X2"));
            }
            return result.ToString();
        }
        ///////////////////////////////////

    }
}
