﻿
namespace GMap.NET.Internals
{
   using System.Collections.Generic;
   using System.Data.SQLite;
   using System.IO;
   using System.Text;

   /// <summary>
   /// cache system for tiles, geocoding, etc...
   /// </summary>
   internal class Cache : Singleton<Cache>, PureImageCache
   {
      string cache;
      public string gtileCache;
      string routeCache;
      string geoCache;
      string placemarkCache;

      /// <summary>
      /// abstract image cache
      /// </summary>
      public PureImageCache ImageCache;

      /// <summary>
      /// second level abstract image cache
      /// </summary>
      public PureImageCache ImageCacheSecond;

      /// <summary>
      /// local cache location
      /// </summary>
      public string CacheLocation
      {
         get
         {
            return cache;
         }
         set
         {
            cache = value;
            gtileCache = cache + "TileDBv3" + Path.DirectorySeparatorChar;
            routeCache = cache + "RouteCache" + Path.DirectorySeparatorChar;
            geoCache = cache + "GeocoderCache" + Path.DirectorySeparatorChar;
            placemarkCache = cache + "PlacemarkCache" + Path.DirectorySeparatorChar;
         }
      }

      public Cache()
      {
         #region singleton check
         if(Instance != null)
         {
            throw (new System.Exception("You have tried to create a new singleton class where you should have instanced it. Replace your \"new class()\" with \"class.Instance\""));
         }
         #endregion

         ImageCache = this;
         CacheLocation = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "GMap.NET" + Path.DirectorySeparatorChar;
      }

      bool CreateEmptyDB(string file)
      {
         bool ret = true;

         try
         {
            string dir = Path.GetDirectoryName(file);
            if(!Directory.Exists(dir))
            {
               Directory.CreateDirectory(dir);
            }

            using(SQLiteConnection cn = new SQLiteConnection())
            {
               cn.ConnectionString = string.Format("Data Source=\"{0}\";FailIfMissing=False;", file);
               cn.Open();
               if(cn.State == System.Data.ConnectionState.Open)
               {
                  using(SQLiteTransaction tr = cn.BeginTransaction())
                  {
                     try
                     {
                        using(SQLiteCommand cmd = new SQLiteCommand(cn))
                        {
                           cmd.CommandText = Properties.Resources.CreateTileDb;
                           cmd.ExecuteNonQuery();
                        }
                        tr.Commit();
                     }
                     catch
                     {
                        tr.Rollback();
                        ret = false;
                     }
                  }
                  cn.Close();
               }
            }
         }
         catch
         {
            ret = false;
         }
         return ret;
      }

      public bool ExportMapDataToDB(string sourceFile, string destFile)
      {
         bool ret = true;

         try
         {
            if(!File.Exists(destFile))
            {
               ret = CreateEmptyDB(destFile);
            }

            if(ret)
            {
               using(SQLiteConnection cn1 = new SQLiteConnection())
               {
                  cn1.ConnectionString = string.Format("Data Source=\"{0}\";", sourceFile);
                  cn1.Open();
                  if(cn1.State == System.Data.ConnectionState.Open)
                  {
                     using(SQLiteConnection cn2 = new SQLiteConnection())
                     {
                        cn2.ConnectionString = string.Format("Data Source=\"{0}\";", destFile);
                        cn2.Open();
                        if(cn2.State == System.Data.ConnectionState.Open)
                        {
                           using(SQLiteCommand cmd = new SQLiteCommand(string.Format("ATTACH DATABASE \"{0}\" AS Source", sourceFile), cn2))
                           {
                              cmd.ExecuteNonQuery();
                           }

                           using(SQLiteTransaction tr = cn2.BeginTransaction())
                           {
                              try
                              {
                                 List<long> add = new List<long>();
                                 using(SQLiteCommand cmd = new SQLiteCommand("SELECT id, X, Y, Zoom, Type FROM Tiles;", cn1))
                                 {
                                    using(SQLiteDataReader rd = cmd.ExecuteReader())
                                    {
                                       while(rd.Read())
                                       {
                                          long id = rd.GetInt64(0);
                                          using(SQLiteCommand cmd2 = new SQLiteCommand(string.Format("SELECT id FROM Tiles WHERE X={0} AND Y={1} AND Zoom={2} AND Type={3};", rd.GetInt32(1), rd.GetInt32(2), rd.GetInt32(3), rd.GetInt32(4)), cn2))
                                          {
                                             using(SQLiteDataReader rd2 = cmd2.ExecuteReader())
                                             {
                                                if(!rd2.Read())
                                                {
                                                   add.Add(id);
                                                }
                                             }
                                          }
                                       }
                                    }
                                 }

                                 foreach(long id in add)
                                 {
                                    using(SQLiteCommand cmd = new SQLiteCommand(string.Format("INSERT INTO Tiles(X, Y, Zoom, Type) SELECT X, Y, Zoom, Type FROM Source.Tiles WHERE id={0}; INSERT INTO TilesData(id, Tile) Values((SELECT last_insert_rowid()), (SELECT Tile FROM Source.TilesData WHERE id={0}));", id), cn2))
                                    {
                                       cmd.ExecuteNonQuery();
                                    }
                                 }
                                 add.Clear();

                                 tr.Commit();
                              }
                              catch
                              {
                                 tr.Rollback();
                                 ret = false;
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
         catch
         {
            ret = false;
         }
         return ret;
      }

      public void CacheGeocoder(string urlEnd, string content)
      {
         try
         {
            // precrete dir
            if(!Directory.Exists(geoCache))
            {
               Directory.CreateDirectory(geoCache);
            }

            StringBuilder file = new StringBuilder(geoCache);
            file.AppendFormat("{0}.geo", urlEnd);

            File.WriteAllText(file.ToString(), content, Encoding.UTF8);
         }
         catch
         {
         }
      }

      public string GetGeocoderFromCache(string urlEnd)
      {
         string ret = null;

         try
         {
            StringBuilder file = new StringBuilder(geoCache);
            file.AppendFormat("{0}.geo", urlEnd);

            if(File.Exists(file.ToString()))
            {
               ret = File.ReadAllText(file.ToString(), Encoding.UTF8);
            }
         }
         catch
         {
            ret = null;
         }

         return ret;
      }

      public void CachePlacemark(string urlEnd, string content)
      {
         try
         {
            // precrete dir
            if(!Directory.Exists(placemarkCache))
            {
               Directory.CreateDirectory(placemarkCache);
            }

            StringBuilder file = new StringBuilder(placemarkCache);
            file.AppendFormat("{0}.plc", urlEnd);

            File.WriteAllText(file.ToString(), content, Encoding.UTF8);
         }
         catch
         {
         }
      }

      public string GetPlacemarkFromCache(string urlEnd)
      {
         string ret = null;

         try
         {
            StringBuilder file = new StringBuilder(placemarkCache);
            file.AppendFormat("{0}.plc", urlEnd);

            if(File.Exists(file.ToString()))
            {
               ret = File.ReadAllText(file.ToString(), Encoding.UTF8);
            }
         }
         catch
         {
            ret = null;
         }

         return ret;
      }

      public void CacheRoute(string urlEnd, string content)
      {
         try
         {
            // precrete dir
            if(!Directory.Exists(routeCache))
            {
               Directory.CreateDirectory(routeCache);
            }

            StringBuilder file = new StringBuilder(routeCache);
            file.AppendFormat("{0}.dragdir", urlEnd);

            File.WriteAllText(file.ToString(), content, Encoding.UTF8);
         }
         catch
         {
         }
      }

      public string GetRouteFromCache(string urlEnd)
      {
         string ret = null;

         try
         {
            StringBuilder file = new StringBuilder(routeCache);
            file.AppendFormat("{0}.dragdir", urlEnd);

            if(File.Exists(file.ToString()))
            {
               ret = File.ReadAllText(file.ToString(), Encoding.UTF8);
            }
         }
         catch
         {
            ret = null;
         }

         return ret;
      }

      #region PureImageCache Members

      bool PureImageCache.PutImageToCache(MemoryStream tile, MapType type, Point pos, int zoom)
      {
         bool ret = true;
         try
         {
            StringBuilder dir = new StringBuilder(gtileCache);
            dir.AppendFormat("{0}{1}", GMaps.Instance.Language, Path.DirectorySeparatorChar);

            string d = dir.ToString();

            // precrete dir
            if(!Directory.Exists(d))
            {
               Directory.CreateDirectory(d);
            }

            // save
            {
               dir.Append("Data.gmdb");

               string db = dir.ToString();
               if(!File.Exists(db))
               {
                  ret = CreateEmptyDB(db);
               }

               if(ret)
               {
                  using(SQLiteConnection cn = new SQLiteConnection())
                  {
                     cn.ConnectionString = string.Format("Data Source=\"{0}\";", db);
                     cn.Open();
                     if(cn.State == System.Data.ConnectionState.Open)
                     {
                        using(tile)
                        {
                           using(SQLiteTransaction tr = cn.BeginTransaction())
                           {
                              try
                              {
                                 using(SQLiteCommand cmd = new SQLiteCommand(cn))
                                 {
                                    cmd.Transaction = tr;

                                    cmd.CommandText = "INSERT INTO Tiles(X, Y, Zoom, Type) VALUES(@p1, @p2, @p3, @p4)";
                                    cmd.Parameters.AddWithValue("@p1", pos.X);
                                    cmd.Parameters.AddWithValue("@p2", pos.Y);
                                    cmd.Parameters.AddWithValue("@p3", zoom);
                                    cmd.Parameters.AddWithValue("@p4", (int) type);

                                    cmd.ExecuteNonQuery();
                                 }

                                 using(SQLiteCommand cmd = new SQLiteCommand(cn))
                                 {
                                    cmd.Transaction = tr;

                                    cmd.CommandText = "INSERT INTO TilesData(id, Tile) VALUES((SELECT last_insert_rowid()), @p1)";
                                    cmd.Parameters.AddWithValue("@p1", tile.GetBuffer());

                                    cmd.ExecuteNonQuery();
                                 }
                                 tr.Commit();
                              }
                              catch
                              {
                                 tr.Rollback();
                                 ret = false;
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
         catch
         {
            ret = false;
         }
         return ret;
      }

      PureImage PureImageCache.GetImageFromCache(MapType type, Point pos, int zoom)
      {
         PureImage ret = null;
         try
         {
            StringBuilder dir = new StringBuilder(gtileCache);
            dir.AppendFormat("{0}{1}Data.gmdb", GMaps.Instance.Language, Path.DirectorySeparatorChar);

            // get
            {
               string db = dir.ToString();
               if(File.Exists(db))
               {
                  using(SQLiteConnection cn = new SQLiteConnection())
                  {
                     cn.ConnectionString = string.Format("Data Source=\"{0}\";", db);
                     cn.Open();
                     if(cn.State == System.Data.ConnectionState.Open)
                     {
                        using(SQLiteCommand com = new SQLiteCommand(cn))
                        {
                           com.CommandText = string.Format("SELECT Tile FROM TilesData WHERE id = (SELECT id FROM Tiles WHERE X={0} AND Y={1} AND Zoom={2} AND Type={3})", pos.X, pos.Y, zoom, (int) type);

                           using(SQLiteDataReader rd = com.ExecuteReader())
                           {
                              if(rd.Read())
                              {
                                 long length = rd.GetBytes(0, 0, null, 0, 0);
                                 byte[] tile = new byte[length];
                                 rd.GetBytes(0, 0, tile, 0, tile.Length);

                                 using(MemoryStream stm = new MemoryStream(tile))
                                 {
                                    if(GMaps.Instance.ImageProxy != null)
                                    {
                                       ret = GMaps.Instance.ImageProxy.FromStream(stm);
                                    }
                                 }
                                 tile = null;
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
         catch
         {
            ret = null;
         }

         return ret;
      }

      #endregion
   }
}
