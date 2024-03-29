﻿using System;
using System.Net;
using System.IO;
using System.Web;
using System.Text;
using System.Data;
using System.Data.SQLite;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

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
            SimpleListenerExample(new string[]{"http://localhost:8000/"}, sqlite_conn);
            sqlite_conn.Close();
        }

        /* etablish connection to db */
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

	    ///////////////////////////////////
	    //////////// DB QUERIES ///////////
	    ///////////////////////////////////

	    /* delete all tables */
	    static void DeleteTable(SQLiteConnection conn)
      	{
	        SQLiteCommand sqlite_cmd;
	        
	        string Createsql = @"
	        	DROP TABLE IF EXISTS Users;
	        	DROP TABLE IF EXISTS Images;
	        ";
	        sqlite_cmd = conn.CreateCommand();

	        sqlite_cmd.CommandText = Createsql;
	        sqlite_cmd.ExecuteNonQuery();
      	}

      	/* create tables to test */
	    static void CreateTable(SQLiteConnection conn)
      	{
	        SQLiteCommand sqlite_cmd;
	        
	        string Createsql = @"
	        	CREATE TABLE Users(Id INT, Username NVARCHAR(255), Password NVARCHAR(255));
	        	CREATE TABLE Images(Id INT, File BLOB);
	        ";
	        sqlite_cmd = conn.CreateCommand();

	        sqlite_cmd.CommandText = Createsql;
	        sqlite_cmd.ExecuteNonQuery();
      	}

      	/* insert data to test */
      	static void InsertData(SQLiteConnection conn)
      	{
         	SQLiteCommand sqlite_cmd;
         	sqlite_cmd = conn.CreateCommand();
         	
         	sqlite_cmd.CommandText = "INSERT INTO Users(Username, Password) VALUES ('username','"+GenerateSHA512String("password")+"');";
         	sqlite_cmd.ExecuteNonQuery();
         	
         	sqlite_cmd.CommandText = "INSERT INTO Users(Username, Password) VALUES ('admin','"+GenerateSHA512String("admin")+"');";
         	sqlite_cmd.ExecuteNonQuery();
      	}

      	/* check what's in db */
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

         	sqlite_cmd = conn.CreateCommand();
         	sqlite_cmd.CommandText = "SELECT * FROM Images";
 		
         	sqlite_datareader = sqlite_cmd.ExecuteReader();
         	while (sqlite_datareader.Read())
         	{
            	Console.WriteLine(sqlite_datareader["File"].ToString());
         	}
      	}

      	///////////////////////////////////
      	////////////// HTTP ///////////////
      	///////////////////////////////////

      	/* HTTP main function : Run server & listen */
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

				List<Image> images = new List<Image>();

				if(url == "/login")
				{
					string[] postCredentials = LoginRequestData(request);
				    if(postCredentials.Length > 0)
				    {
				    	Console.WriteLine(postCredentials[0]);
				    	Console.WriteLine(postCredentials[1]);
				    	if(IsUserValid(sqlite_conn, postCredentials[0], postCredentials[1]))
				    	{
				    		Console.WriteLine("Credentials OK");
				    		SetSession();
				    		response.Redirect("http://localhost:8000/upload");
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
			    		response.Redirect("http://localhost:8000/login");
			    	}
			    	String fileUpload = UploadRequestData(request);
			    	Console.WriteLine(fileUpload);
			    	if(fileUpload.Length > 0)
			    	{
			    		InsertFileDatabase(sqlite_conn, fileUpload);
			    		response.Redirect("http://localhost:8000/gallery");
			    	}
			    }
			    else if(url == "/gallery")
			    {
			    	List<byte[]> byteImages = GetGalleryPhotos(sqlite_conn);

			    	for(int i = 0; i < byteImages.Count; i++)
			    	{
			    		images.Add(ByteToImage(byteImages[i]));
			    	}
			    	Console.WriteLine(images);
			    }

				RenderHtml(url, response, images);
			}
		}

		/* display html according to url requested */
		static void RenderHtml(String url, HttpListenerResponse response, List<Image> images)
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

				case "/gallery":
				responseString = @"
				    <html>
				    	<body>
				    	Gallery
				    		"+ images[0] +@"
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
		///////////////////////////////////

		/* get login inputs submitted by user */
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

		/* check if user exists in db */
		static bool IsUserValid(SQLiteConnection conn, string username, string password)
      	{
	        string sql = "SELECT * FROM Users WHERE username LIKE @username AND password LIKE @password";
	 			
			using (conn)
			using (SQLiteCommand sqlite_cmd = new SQLiteCommand(sql, conn))
			{			    
			    sqlite_cmd.Parameters.AddWithValue("@username", HttpUtility.UrlDecode(username));
			    sqlite_cmd.Parameters.AddWithValue("@password", GenerateSHA512String(HttpUtility.UrlDecode(password)));

			    SQLiteDataReader sqlite_datareader = sqlite_cmd.ExecuteReader();
			    while (sqlite_datareader.Read())
	         	{
	            	return true;
	         	}
			}			
         	
         	return false;
      	}

      	///////////////////////////////////
      	////////////// UPLOAD /////////////
      	///////////////////////////////////

      	/* return user file uploaded */
		static String UploadRequestData(HttpListenerRequest request)
		{
		    if (!request.HasEntityBody)
		    {
		        Console.WriteLine("No client data was sent with the request.");
		        return new String("");
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

		/* insert user file into db */
		static void InsertFileDatabase(SQLiteConnection conn, String fileUpload)
		{
			Image img = Image.FromFile("images/"+fileUpload);
            MemoryStream tmpStream = new MemoryStream();
            img.Save (tmpStream, ImageFormat.Png);
            tmpStream.Seek (0, SeekOrigin.Begin);
            byte[] imgBytes = new byte[2000000];
            tmpStream.Read (imgBytes, 0, 2000000);

			string sql = "INSERT INTO Images(file) VALUES(@file)";
			
			using (conn)
			using (var sqlite_cmd = new SQLiteCommand(sql, conn))
	        {
	            sqlite_cmd.Parameters.AddWithValue("@file", imgBytes);
				sqlite_cmd.ExecuteNonQuery();
	        }
		}

      	///////////////////////////////////
      	///////////// GALLERY /////////////
      	///////////////////////////////////

      	/* return all the images of the gallery */
      	static List<byte[]> GetGalleryPhotos(SQLiteConnection conn)
      	{
      		List<byte[]> images = new List<byte[]>();

      		SQLiteDataReader sqlite_datareader;
         	SQLiteCommand sqlite_cmd;
         	
         	sqlite_cmd = conn.CreateCommand();
         	sqlite_cmd.CommandText = "SELECT * FROM Images";
 		
         	sqlite_datareader = sqlite_cmd.ExecuteReader();
         	while (sqlite_datareader.Read())
         	{
            	images.Add((Byte[])(sqlite_datareader["File"]));
         	}

         	return images;
      	}

      	/* transform data of blob into readable image */
      	public static Bitmap ByteToImage(byte[] blob)
		{
		    MemoryStream mStream = new MemoryStream();
		    byte[] pData = blob;

		    mStream.Write(pData, 0, Convert.ToInt32(pData.Length));
		    Bitmap bm = new Bitmap(mStream, false);
		    mStream.Dispose();

		    return bm;
		}

		///////////////////////////////////
      	//////////////// HASH /////////////
      	///////////////////////////////////

      	/* create hash with sha256 */
      	public static string GenerateSHA256String(string inputString)
        {
            SHA256 sha256 = SHA256Managed.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(inputString);
            byte[] hash = sha256.ComputeHash(bytes);
            return GetStringFromHash(hash);
        }

        /* create hash with sha512 */
        public static string GenerateSHA512String(string inputString)
        {
            SHA512 sha512 = SHA512Managed.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(inputString);
            byte[] hash = sha512.ComputeHash(bytes);
            return GetStringFromHash(hash);
        }

        /* return the string of an hash */
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
      	////////////// SESSION ////////////
      	///////////////////////////////////

      	/* set session for user */
      	static void SetSession()
      	{
      		// Not implemented
      	}

		/* check if user is connected */
      	static bool CheckSession()
      	{
      		// Not implemented
      		return true;
      	}

    }
}
