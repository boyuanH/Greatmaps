
namespace GMap.NET.Internals
{
   using System;
   using System.Collections.Generic;
   using System.Diagnostics;
   using System.Threading;

#if PocketPC
   using OpenNETCF.ComponentModel;
   using OpenNETCF.Threading;
   using Thread=OpenNETCF.Threading.Thread2;
   //using Monitor=OpenNETCF.Threading.Monitor2;
#endif

#if PocketPC
   class Monitor
   {
      static readonly Monitor2 wait = new Monitor2();

      public static bool Wait(Queue<LoadTask> tileLoadQueue, int WaitForTileLoadThreadTimeout, bool p)
      {
         wait.Wait();
         return true;
      }

      internal static void PulseAll(Queue<LoadTask> tileLoadQueue)
      {
         wait.PulseAll();
      }
   }
#endif

   /// <summary>
   /// internal map control core
   /// </summary>
   internal class Core
   {
      public GPoint renderOffset;
      public GPoint centerTileXY;
      public GPoint centerTileXYLast;
      public GPoint dragPoint;

      public GPoint mouseDown;
      public GPoint mouseCurrent;
      public GPoint mouseLastZoom;

      public MouseWheelZoomType MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter;

      public PointLatLng? LastLocationInBounds = null;
      public bool VirtualSizeEnabled = false;

      public GSize sizeOfMapArea;
      public GSize minOfTiles;
      public GSize maxOfTiles;
      public GSize matrixSizePixel;

      public GRect tileRect;
      public GRect tileRectBearing;
      public GRect currentRegion;
      public float bearing = 0;
      public bool IsRotated = false;

      public readonly TileMatrix Matrix = new TileMatrix();

      public readonly List<GPoint> tileDrawingList = new List<GPoint>();
      public readonly FastReaderWriterLock tileDrawingListLock = new FastReaderWriterLock();

      public readonly Queue<LoadTask> tileLoadQueue = new Queue<LoadTask>();

      public static readonly string googleCopyright = string.Format("©{0} Google - Map data ©{0} Tele Atlas, Imagery ©{0} TerraMetrics", DateTime.Today.Year);
      public static readonly string openStreetMapCopyright = string.Format("© OpenStreetMap - Map data ©{0} OpenStreetMap", DateTime.Today.Year);
      public static readonly string yahooMapCopyright = string.Format("© Yahoo! Inc. - Map data & Imagery ©{0} NAVTEQ", DateTime.Today.Year);
      public static readonly string virtualEarthCopyright = string.Format("©{0} Microsoft Corporation, ©{0} NAVTEQ, ©{0} Image courtesy of NASA", DateTime.Today.Year);
      public static readonly string arcGisCopyright = string.Format("©{0} ESRI - Map data ©{0} ArcGIS", DateTime.Today.Year);
      public static readonly string hnitCopyright = string.Format("©{0} Hnit-Baltic - Map data ©{0} ESRI", DateTime.Today.Year);
      public static readonly string pergoCopyright = string.Format("©{0} Pergo - Map data ©{0} Fideltus Advanced Technology", DateTime.Today.Year);

#if !PocketPC
      static readonly int GThreadPoolSize = 5;
#else
      static readonly int GThreadPoolSize = 2;
#endif

      DateTime LastInvalidation = DateTime.Now;
      DateTime LastTileLoadStart = DateTime.Now;
      DateTime LastTileLoadEnd = DateTime.Now;
      internal bool IsStarted = false;
      int zoom;
      internal int maxZoom = 2;
      internal int minZoom = 2;
      internal int Width;
      internal int Height;

      internal volatile int pxRes100m;  // 100 meters
      internal volatile int pxRes1000m;  // 1km  
      internal volatile int pxRes10km; // 10km
      internal volatile int pxRes100km; // 100km
      internal volatile int pxRes1000km; // 1000km
      internal volatile int pxRes5000km; // 5000km

      /// <summary>
      /// current peojection
      /// </summary>
      public PureProjection Projection;

      /// <summary>
      /// is user dragging map
      /// </summary>
      public bool IsDragging = false;

      public Core()
      {
         MapType = MapType.None;
      }

      public GPoint virtualOrignPixel;
      public int RenderMax = 512;

      public void AdjustToVirtualSpace(bool resetOrign)
      {
         if(resetOrign)
         {
            virtualOrignPixel = GPoint.Empty;
         }

         virtualOrignPixel = new GPoint(64, 64);

         //int diff = GPoint.MaxDiffOfXY(virtualOrignPixel, centerPixel);
         //if(diff > RenderMax) // more than 100k px, the limits of current render systems
         //{
         //   Debug.WriteLine("AdjustToVirtualSpace: diff high " + diff + "px, " + centerPixel + " <- " + virtualOrignPixel);
         //   //virtualOrignPixel = centerPixel;
         //}
         //else
         //{
         //   if(GPoint.MaxOfXY(centerPixel) < RenderMax)
         //   {
         //      //virtualOrignPixel = GPoint.Empty;
         //      Debug.WriteLine("AdjustToVirtualSpace: diff good " + diff + "px, " + virtualOrignPixel);
         //   }
         //}
      }

      public void UpdateFieldsOnZoomChanged(int value)
      {
         if(zoomPositionChanged)
         {
            zoomPositionChanged = false;
            zoomPosition = Projection.FromPixelToLatLng(centerPixelVirtual, zoom, true);
         }
         minOfTiles = Projection.GetTileMatrixMinXY(value);
         maxOfTiles = Projection.GetTileMatrixMaxXY(value);
         centerPixel = Projection.FromLatLngToPixel(zoomPosition, value, true);
         zoom = value;
         matrixSizePixel = Projection.GetTileMatrixSizePixel(value);

         AdjustToVirtualSpace(true);

         Debug.WriteLine("MatrixSizePixel: " + matrixSizePixel);
         Debug.WriteLine("ZoomPositionPixel: " + centerPixel);
      }

      /// <summary>
      /// map zoom
      /// </summary>
      public int Zoom
      {
         get
         {
            return zoom;
         }
         set
         {
            if(zoom != value && !IsDragging)
            {
               Debug.WriteLine("----------------------------------------------");
               Debug.WriteLine("ZoomChanging: " + zoom + " -> " + value);

               UpdateFieldsOnZoomChanged(value);

               if(IsStarted)
               {
                  lock(tileLoadQueue)
                  {
                     tileLoadQueue.Clear();
                  }

                  Matrix.ClearLevelsBelove(zoom - LevelsKeepInMemmory);
                  Matrix.ClearLevelsAbove(zoom + LevelsKeepInMemmory);

                  lock(FailedLoads)
                  {
                     FailedLoads.Clear();
                     RaiseEmptyTileError = true;
                  }

                  GoToCurrentPositionOnZoom();
                  UpdateBounds();

                  if(OnMapZoomChanged != null)
                  {
                     OnMapZoomChanged();
                  }
               }
            }
         }
      }

      PointLatLng zoomPosition;
      public bool zoomPositionChanged = false;

      /// <summary>
      /// current marker position
      /// </summary>
      public PointLatLng CurrentPosition
      {
         get
         {
            return Projection.FromPixelToLatLng(centerPixel, zoom, true);
         }
         set
         {
            zoomPosition = value;
            centerPixel = Projection.FromLatLngToPixel(zoomPosition, Zoom, true);
            zoomPositionChanged = false;

            AdjustToVirtualSpace(false);

            if(!IsDragging)
            {
               if(IsStarted)
               {
                  GoToCurrentPosition(true);
               }
            }

            if(IsStarted)
            {
               if(OnCurrentPositionChanged != null)
                  OnCurrentPositionChanged(value);
            }
         }
      }

      internal RectLatLng? zoomArea;

      MapType mapType;
      public MapType MapType
      {
         get
         {
            return mapType;
         }
         set
         {
            if(value != MapType || value == MapType.None)
            {
               mapType = value;
               if(Projection != null)
               {
                  zoomPosition = Projection.FromPixelToLatLng(centerPixel, zoom, true);
               }
               GMaps.Instance.AdjustProjection(mapType, ref Projection, out maxZoom);

               tileRect = new GRect(new GPoint(0, 0), Projection.TileSize);
               tileRectBearing = tileRect;
               if(IsRotated)
               {
                  tileRectBearing.Inflate(1, 1);
               }

               zoomPositionChanged = false;

               if(IsStarted)
               {
                  UpdateCenterTileXYLocation();
                  zoomArea = null;

                  #region -- get zoom area if needed --
                  switch(mapType)
                  {
                     case MapType.MapsLT_Map_Hybrid:
                     case MapType.MapsLT_Map_Labels:
                     case MapType.MapsLT_Map:
                     case MapType.MapsLT_OrtoFoto:
                     case MapType.MapsLT_Map_2_5D:
                     case MapType.MapsLT_Map_Hybrid_2010:
                     case MapType.MapsLT_OrtoFoto_2010:
                     {
                        RectLatLng area = new RectLatLng(56.431489960361, 20.8962105239809, 5.8924169643369, 2.58940626652217);
                        if(!area.Contains(zoomPosition))
                        {
                           zoomArea = area;
                        }
                     }
                     break;

                     case MapType.KarteLV_Map:
                     {
                        RectLatLng area = new RectLatLng(58.0794870805093, 20.3286067123543, 7.90883164336887, 2.506129113082);
                        if(!area.Contains(zoomPosition))
                        {
                           zoomArea = area;
                        }
                     }
                     break;

                     case MapType.MapyCZ_Map:
                     case MapType.MapyCZ_Satellite:
                     case MapType.MapyCZ_MapTurist:
                     case MapType.MapyCZ_Labels:
                     case MapType.MapyCZ_Hybrid:
                     case MapType.MapyCZ_History:
                     case MapType.MapyCZ_HistoryHybrid:
                     {
                        RectLatLng area = new RectLatLng(51.2024819920053, 11.8401353319027, 7.22833716731277, 2.78312271922872);
                        if(!area.Contains(zoomPosition))
                        {
                           zoomArea = area;
                        }
                     }
                     break;

                     case MapType.PergoTurkeyMap:
                     {
                        RectLatLng area = new RectLatLng(42.5830078125, 25.48828125, 19.05029296875, 6.83349609375);
                        if(!area.Contains(zoomPosition))
                        {
                           zoomArea = area;
                        }
                     }
                     break;

                     case MapType.SigPacSpainMap:
                     {
                        if(minZoom < 5)
                        {
                           minZoom = 5;
                        }

                        RectLatLng area = new RectLatLng(43.8741381814747, -9.700927734375, 14.34814453125, 7.8605775962932);
                        if(!area.Contains(zoomPosition))
                        {
                           zoomArea = area;
                        }
                     }
                     break;

                     case MapType.GoogleMapKorea:
                     case MapType.GoogleLabelsKorea:
                     case MapType.GoogleHybridKorea:
                     case MapType.GoogleSatelliteKorea:
                     {
                        RectLatLng area = new RectLatLng(38.6597777307125, 125.738525390625, 4.02099609375, 4.42072406219614);
                        if(!area.Contains(zoomPosition))
                        {
                           zoomArea = area;
                        }
                     }
                     break;
                  }
                  #endregion

                  if(zoomArea.HasValue)
                  {
                     zoomPosition = zoomArea.Value.LocationMiddle;
                  }
               }
            }
         }
      }

      /// <summary>
      /// sets zoom to max to fit rect
      /// </summary>
      /// <param name="rect"></param>
      /// <returns></returns>
      public bool SetZoomToFitRect(RectLatLng rect)
      {
         int mmaxZoom = GetMaxZoomToFitRect(rect);
         if(mmaxZoom > 0)
         {
            PointLatLng center = new PointLatLng(rect.Lat - (rect.HeightLat / 2), rect.Lng + (rect.WidthLng / 2));
            CurrentPosition = center;

            if(mmaxZoom > maxZoom)
            {
               mmaxZoom = maxZoom;
            }

            if((int)Zoom != mmaxZoom)
            {
               Zoom = mmaxZoom;
            }

            return true;
         }
         return false;
      }

      /// <summary>
      /// is polygons enabled
      /// </summary>
      public bool PolygonsEnabled = true;

      /// <summary>
      /// is routes enabled
      /// </summary>
      public bool RoutesEnabled = true;

      /// <summary>
      /// is markers enabled
      /// </summary>
      public bool MarkersEnabled = true;

      /// <summary>
      /// can user drag map
      /// </summary>
      public bool CanDragMap = true;

      /// <summary>
      /// retry count to get tile 
      /// </summary>
#if !PocketPC
      public int RetryLoadTile = 0;
#else
      public int RetryLoadTile = 1;
#endif

      /// <summary>
      /// how many levels of tiles are staying decompresed in memory
      /// </summary>
#if !PocketPC
      public int LevelsKeepInMemmory = 5;
#else
      public int LevelsKeepInMemmory = 1;
#endif

      /// <summary>
      /// map render mode
      /// </summary>
      public RenderMode RenderMode = RenderMode.GDI_PLUS;

      /// <summary>
      /// occurs when current position is changed
      /// </summary>
      public event CurrentPositionChanged OnCurrentPositionChanged;

      /// <summary>
      /// occurs when tile set load is complete
      /// </summary>
      public event TileLoadComplete OnTileLoadComplete;

      /// <summary>
      /// occurs when tile set is starting to load
      /// </summary>
      public event TileLoadStart OnTileLoadStart;

      /// <summary>
      /// occurs on empty tile displayed
      /// </summary>
      public event EmptyTileError OnEmptyTileError;

      /// <summary>
      /// occurs on tile loaded
      /// </summary>
      public event NeedInvalidation OnNeedInvalidation;

      /// <summary>
      /// occurs on map drag
      /// </summary>
      public event MapDrag OnMapDrag;

      /// <summary>
      /// occurs on map zoom changed
      /// </summary>
      public event MapZoomChanged OnMapZoomChanged;

      /// <summary>
      /// occurs on map type changed
      /// </summary>
      public event MapTypeChanged OnMapTypeChanged;

      readonly List<Thread> GThreadPool = new List<Thread>();

      // windows forms or wpf
      internal string SystemType;
      internal static readonly Guid SessionIdGuid = Guid.NewGuid();
      internal static readonly Guid CompanyIdGuid = new Guid("3E35F098-CE43-4F82-9E9D-05C8B1046A45");
      internal static readonly Guid ApplicationIdGuid = new Guid("FF328040-77B0-4546-ACF3-7C6EC0827BBB");
      internal static volatile bool AnalyticsStartDone = false;
      internal static volatile bool AnalyticsStopDone = false;

      /// <summary>
      /// starts core system
      /// </summary>
      public void StartSystem()
      {
         if(!IsStarted)
         {
            IsStarted = true;
            GoToCurrentPosition(true);
#if !DEBUG
#if !PocketPC
            // in case there a few controls in one app
            if(!AnalyticsStartDone)
            {
               AnalyticsStartDone = true;

               // send start ping to codeplex Analytics service
               ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o)
               {
                  try
                  {
                     using(Analytics.MessagingServiceV2 s = new Analytics.MessagingServiceV2())
                     {
                        if(GMaps.Instance.Proxy != null)
                        {
                           s.Proxy = GMaps.Instance.Proxy;
                           s.PreAuthenticate = true;
                        }

                        Analytics.MessageCache info = new Analytics.MessageCache();
                        {
                           FillAnalyticsInfo(info);

                           info.Messages = new Analytics.Message[2];

                           Analytics.ApplicationLifeCycle alc = new Analytics.ApplicationLifeCycle();
                           {
                              alc.Id = Guid.NewGuid();
                              alc.SessionId = SessionIdGuid;
                              alc.TimeStampUtc = DateTime.UtcNow;

                              alc.Event = new GMap.NET.Analytics.EventInformation();
                              {
                                 alc.Event.Code = "Application.Start";
                                 alc.Event.PrivacySetting = GMap.NET.Analytics.PrivacySettings.SupportOptout;
                              }

                              alc.Binary = new Analytics.BinaryInformation();
                              {
                                 System.Reflection.AssemblyName app = System.Reflection.Assembly.GetEntryAssembly().GetName();
                                 alc.Binary.Name = app.Name;
                                 alc.Binary.Version = app.Version.ToString();
                              }

                              alc.Host = new GMap.NET.Analytics.HostInfo();
                              {
                                 alc.Host.RuntimeVersion = Environment.Version.ToString();
                              }

                              alc.Host.OS = new GMap.NET.Analytics.OSInformation();
                              {
                                 alc.Host.OS.OsName = Environment.OSVersion.VersionString;
                              }
                           }
                           info.Messages[0] = alc;

                           Analytics.SessionLifeCycle slc = new Analytics.SessionLifeCycle();
                           {
                              slc.Id = Guid.NewGuid();
                              slc.SessionId = SessionIdGuid;
                              slc.TimeStampUtc = DateTime.UtcNow;

                              slc.Event = new GMap.NET.Analytics.EventInformation();
                              {
                                 slc.Event.Code = "Session.Start";
                                 slc.Event.PrivacySetting = GMap.NET.Analytics.PrivacySettings.SupportOptout;
                              }
                           }
                           info.Messages[1] = slc;
                        }
                        s.Publish(info);
                     }
                  }
                  catch(Exception ex)
                  {
                     Debug.WriteLine("Analytics Start: " + ex.ToString());
                  }
               }));
            }
#endif
#endif
         }
      }

      internal void ApplicationExit()
      {
#if !DEBUG
#if !PocketPC
         // send end ping to codeplex Analytics service
         try
         {
            if(!AnalyticsStopDone)
            {
               AnalyticsStopDone = true;

               using(Analytics.MessagingServiceV2 s = new Analytics.MessagingServiceV2())
               {
                  s.Timeout = 10 * 1000;

                  if(GMaps.Instance.Proxy != null)
                  {
                     s.Proxy = GMaps.Instance.Proxy;
                     s.PreAuthenticate = true;
                  }

                  Analytics.MessageCache info = new Analytics.MessageCache();
                  {
                     FillAnalyticsInfo(info);

                     info.Messages = new Analytics.Message[2];

                     Analytics.SessionLifeCycle slc = new Analytics.SessionLifeCycle();
                     {
                        slc.Id = Guid.NewGuid();
                        slc.SessionId = SessionIdGuid;
                        slc.TimeStampUtc = DateTime.UtcNow;

                        slc.Event = new GMap.NET.Analytics.EventInformation();
                        {
                           slc.Event.Code = "Session.Stop";
                           slc.Event.PrivacySetting = GMap.NET.Analytics.PrivacySettings.SupportOptout;
                        }
                     }
                     info.Messages[0] = slc;

                     Analytics.ApplicationLifeCycle alc = new Analytics.ApplicationLifeCycle();
                     {
                        alc.Id = Guid.NewGuid();
                        alc.SessionId = SessionIdGuid;
                        alc.TimeStampUtc = DateTime.UtcNow;

                        alc.Event = new GMap.NET.Analytics.EventInformation();
                        {
                           alc.Event.Code = "Application.Stop";
                           alc.Event.PrivacySetting = GMap.NET.Analytics.PrivacySettings.SupportOptout;
                        }

                        alc.Binary = new Analytics.BinaryInformation();
                        {
                           System.Reflection.AssemblyName app = System.Reflection.Assembly.GetEntryAssembly().GetName();
                           alc.Binary.Name = app.Name;
                           alc.Binary.Version = app.Version.ToString();
                        }

                        alc.Host = new GMap.NET.Analytics.HostInfo();
                        {
                           alc.Host.RuntimeVersion = Environment.Version.ToString();
                        }

                        alc.Host.OS = new GMap.NET.Analytics.OSInformation();
                        {
                           alc.Host.OS.OsName = Environment.OSVersion.VersionString;
                        }
                     }
                     info.Messages[1] = alc;
                  }
                  s.Publish(info);
               }
            }
         }
         catch(Exception ex)
         {
            Debug.WriteLine("Analytics Stop: " + ex.ToString());
         }
#endif
#endif
      }

#if !PocketPC
      void FillAnalyticsInfo(Analytics.MessageCache info)
      {
         info.SchemaVersion = GMap.NET.Analytics.SchemaVersionValue.Item020000;

         info.Id = Guid.NewGuid();
         info.ApplicationGroupId = SessionIdGuid;

         info.Business = new GMap.NET.Analytics.BusinessInformation();
         info.Business.CompanyId = CompanyIdGuid;
         info.Business.CompanyName = "email@radioman.lt";

         info.TimeSentUtc = DateTime.UtcNow;

         info.ApiLanguage = ".NET CLR";
         info.ApiVersion = "2.1.4.0";

         info.Application = new Analytics.ApplicationInformation();
         info.Application.Id = ApplicationIdGuid;
         info.Application.Name = "GMap.NET";
         info.Application.ApplicationType = SystemType;
         info.Application.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
      }
#endif

      public GRect viewRectPixel;
      public GRect viewRectPixelInflated;
      public GPoint centerPixel;
      public GPoint centerPixelVirtual;

      public void UpdateViewRect()
      {
         viewRectPixel = new GRect(-renderOffset.X, -renderOffset.Y, Width, Height);
         viewRectPixelInflated = viewRectPixel;
         viewRectPixelInflated.Inflate(55, 55);

         centerPixel = viewRectPixel.Location;
         centerPixel.Offset(Width / 2, Height / 2);

         centerPixelVirtual = centerPixel;
         centerPixelVirtual.Offset(virtualOrignPixel);
      }

      public void UpdateCenterTileXYLocation()
      {
         UpdateViewRect();
         centerTileXY = Projection.FromPixelToTileXY(centerPixelVirtual);
      }

      public void OnMapSizeChanged(int width, int height, bool updateBounds)
      {
         this.Width = width;
         this.Height = height;

         if(IsRotated)
         {
#if !PocketPC
            int diag = (int)Math.Round(Math.Sqrt(Width * Width + Height * Height) / Projection.TileSize.Width, MidpointRounding.AwayFromZero);
#else
            int diag = (int) Math.Round(Math.Sqrt(Width * Width + Height * Height) / Projection.TileSize.Width);
#endif
            sizeOfMapArea.Width = 1 + (diag / 2);
            sizeOfMapArea.Height = 1 + (diag / 2);
         }
         else
         {
            sizeOfMapArea.Width = 1 + (Width / Projection.TileSize.Width) / 2;
            sizeOfMapArea.Height = 1 + (Height / Projection.TileSize.Height) / 2;
         }

         Debug.WriteLine("OnMapSizeChanged, w: " + width + ", h: " + height + ", size: " + sizeOfMapArea);

         if(IsStarted && updateBounds)
         {
            UpdateBounds();
         }
      }

      public void OnMapClose()
      {
         CancelAsyncTasks();
         IsStarted = false;

         Matrix.ClearAllLevels();

         lock(FailedLoads)
         {
            FailedLoads.Clear();
            RaiseEmptyTileError = false;
         }
      }

      /// <summary>
      /// gets current map view top/left coordinate, width in Lng, height in Lat
      /// </summary>
      /// <returns></returns>
      public RectLatLng CurrentViewArea
      {
         get
         {
            if(Projection != null)
            {
               PointLatLng p = Projection.FromPixelToLatLng(viewRectPixel.X, viewRectPixel.Y, Zoom);
               PointLatLng p2 = Projection.FromPixelToLatLng(viewRectPixel.Right, viewRectPixel.Bottom, Zoom);

               return RectLatLng.FromLTRB(p.Lng, p.Lat, p2.Lng, p2.Lat);
            }
            return RectLatLng.Empty;
         }
      }

      /// <summary>
      /// gets lat/lng from local control coordinates
      /// </summary>
      /// <param name="x"></param>
      /// <param name="y"></param>
      /// <returns></returns>
      public PointLatLng FromLocalToLatLng(int x, int y)
      {
         return Projection.FromPixelToLatLng(new GPoint(x - renderOffset.X, y - renderOffset.Y), Zoom, true);
      }

      /// <summary>
      /// gets max zoom level to fit rectangle
      /// </summary>
      /// <param name="rect"></param>
      /// <returns></returns>
      public int GetMaxZoomToFitRect(RectLatLng rect)
      {
         int zoom = minZoom;

         for(int i = zoom; i <= maxZoom; i++)
         {
            GPoint p1 = Projection.FromLatLngToPixel(rect.LocationTopLeft, i, true);
            GPoint p2 = Projection.FromLatLngToPixel(rect.LocationRightBottom, i, true);

            if(((p2.X - p1.X) <= Width + 10) && (p2.Y - p1.Y) <= Height + 10)
            {
               zoom = i;
            }
            else
            {
               break;
            }
         }

         return zoom;
      }

      /// <summary>
      /// initiates map dragging
      /// </summary>
      /// <param name="pt"></param>
      public void BeginDrag(GPoint pt)
      {
         dragPoint.X = pt.X - renderOffset.X;
         dragPoint.Y = pt.Y - renderOffset.Y;
         IsDragging = true;
      }

      /// <summary>
      /// ends map dragging
      /// </summary>
      public void EndDrag()
      {
         IsDragging = false;
         zoomPositionChanged = true;

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// reloads map
      /// </summary>
      public void ReloadMap()
      {
         if(IsStarted)
         {
            Debug.WriteLine("------------------");

            lock(tileLoadQueue)
            {
               tileLoadQueue.Clear();
            }

            Projection.ClearCache();
            Matrix.ClearAllLevels();

            lock(FailedLoads)
            {
               FailedLoads.Clear();
               RaiseEmptyTileError = true;
            }

            zoomPositionChanged = false;

            if(OnNeedInvalidation != null)
            {
               OnNeedInvalidation();
            }

            UpdateBounds();
         }
         else
         {
            throw new Exception("Please, do not call ReloadMap before form is loaded, it's useless");
         }
      }

      /// <summary>
      /// moves current position into map center
      /// </summary>
      public void GoToCurrentPosition(bool useVirtualOrign)
      {
         renderOffset = GPoint.Empty;
         centerTileXYLast = GPoint.Empty;
         dragPoint = GPoint.Empty;

         if(!useVirtualOrign)
         {
            Drag(new GPoint(-centerPixel.X + Width / 2, -centerPixel.Y + Height / 2));
         }
         else
         {
            Drag(new GPoint(virtualOrignPixel.X - centerPixel.X + Width / 2, virtualOrignPixel.Y - centerPixel.Y + Height / 2));
         }
      }

      public bool MouseWheelZooming = false;

      /// <summary>
      /// moves current position into map center
      /// </summary>
      internal void GoToCurrentPositionOnZoom()
      {
         renderOffset = GPoint.Empty;
         centerTileXYLast = GPoint.Empty;
         dragPoint = GPoint.Empty;

         // goto location and centering
         if(MouseWheelZooming)
         {
            if(MouseWheelZoomType != MouseWheelZoomType.MousePositionWithoutCenter)
            {
               //GPoint pt = new GPoint(-(centerPixel.X - Width / 2) + virtualOrignPixel.X, -(centerPixel.Y - Height / 2) + virtualOrignPixel.Y);
               //renderOffset.X = pt.X - dragPoint.X;
               //renderOffset.Y = pt.Y - dragPoint.Y;

               mouseLastZoom = GPoint.Empty;

               renderOffset.X = virtualOrignPixel.X - centerPixel.X + Width / 2;
               renderOffset.Y = virtualOrignPixel.Y - centerPixel.Y + Height / 2;
            }
            else // without centering
            {
               renderOffset.X = -centerPixel.X - dragPoint.X;
               renderOffset.Y = -centerPixel.Y - dragPoint.Y;
               renderOffset.Offset(mouseLastZoom);
            }
         }
         else // use current map center
         {
            mouseLastZoom = GPoint.Empty;

            renderOffset.X = virtualOrignPixel.X - centerPixel.X + Width / 2;
            renderOffset.Y = virtualOrignPixel.Y - centerPixel.Y + Height / 2;
         }

         UpdateCenterTileXYLocation();
      }

      /// <summary>
      /// darg map by offset in pixels
      /// </summary>
      /// <param name="offset"></param>
      public void DragOffset(GPoint offset)
      {
         renderOffset.Offset(offset);

         UpdateCenterTileXYLocation();

         if(centerTileXY != centerTileXYLast)
         {
            centerTileXYLast = centerTileXY;
            UpdateBounds();
         }

         {
            //LastLocationInBounds = CurrentPosition;
            //CurrentPosition = FromLocalToLatLng((int) Width / 2, (int) Height / 2);
         }

         if(OnMapDrag != null)
         {
            OnMapDrag();
         }
      }

      /// <summary>
      /// drag map
      /// </summary>
      /// <param name="pt"></param>
      public void Drag(GPoint pt)
      {
         renderOffset.X = pt.X - dragPoint.X;
         renderOffset.Y = pt.Y - dragPoint.Y;

         UpdateCenterTileXYLocation();

         if(centerTileXY != centerTileXYLast)
         {
            centerTileXYLast = centerTileXY;
            UpdateBounds();
         }

         if(IsDragging)
         {
            //LastLocationInBounds = CurrentPosition;
            if(OnMapDrag != null)
            {
               OnMapDrag();
            }
         }
      }

      /// <summary>
      /// cancels tile loaders and bounds checker
      /// </summary>
      public void CancelAsyncTasks()
      {
         if(IsStarted)
         {
            lock(tileLoadQueue)
            {
               tileLoadQueue.Clear();
            }
         }
      }

      bool RaiseEmptyTileError = false;
      internal readonly Dictionary<LoadTask, Exception> FailedLoads = new Dictionary<LoadTask, Exception>();

      internal static readonly int WaitForTileLoadThreadTimeout = 5 * 1000 * 60; // 5 min.

      long loadWaitCount = 0;
      readonly object LastInvalidationLock = new object();
      readonly object LastTileLoadStartEndLock = new object();

      void ProcessLoadTask()
      {
         bool invalidate = false;
         LoadTask? task = null;
         long lastTileLoadTimeMs;
         bool stop = false;

#if !PocketPC
         Thread ct = Thread.CurrentThread;
         string ctid = "Thread[" + ct.ManagedThreadId + "]";
#else
         int ctid = 0;
#endif
         while(!stop)
         {
            invalidate = false;
            task = null;

            lock(tileLoadQueue)
            {
               while(tileLoadQueue.Count == 0)
               {
                  Debug.WriteLine(ctid + " - Wait " + loadWaitCount + " - " + DateTime.Now.TimeOfDay);

                  if(++loadWaitCount >= GThreadPoolSize)
                  {
                     loadWaitCount = 0;

                     lock(LastInvalidationLock)
                     {
                        LastInvalidation = DateTime.Now;
                     }

                     if(OnNeedInvalidation != null)
                     {
                        OnNeedInvalidation();
                     }

                     lock(LastTileLoadStartEndLock)
                     {
                        LastTileLoadEnd = DateTime.Now;
                        lastTileLoadTimeMs = (long)(LastTileLoadEnd - LastTileLoadStart).TotalMilliseconds;
                     }

                     #region -- clear stuff--
                     {
                        GMaps.Instance.kiberCacheLock.AcquireWriterLock();
                        try
                        {
                           GMaps.Instance.TilesInMemory.RemoveMemoryOverload();
                        }
                        finally
                        {
                           GMaps.Instance.kiberCacheLock.ReleaseWriterLock();
                        }

                        tileDrawingListLock.AcquireReaderLock();
                        try
                        {
                           Matrix.ClearLevelAndPointsNotIn(Zoom, tileDrawingList);
                        }
                        finally
                        {
                           tileDrawingListLock.ReleaseReaderLock();
                        }
                     }
                     #endregion

                     UpdateGroundResolution();
#if UseGC
                     GC.Collect();
                     GC.WaitForPendingFinalizers();
                     GC.Collect();
#endif

                     Debug.WriteLine(ctid + " - OnTileLoadComplete: " + lastTileLoadTimeMs + "ms, MemoryCacheSize: " + GMaps.Instance.MemoryCacheSize + "MB");

                     if(OnTileLoadComplete != null)
                     {
                        OnTileLoadComplete(lastTileLoadTimeMs);
                     }
                  }

                  if(false == Monitor.Wait(tileLoadQueue, WaitForTileLoadThreadTimeout, false))
                  {
                     stop = true;
                     break;
                  }
               }

               if(!stop || tileLoadQueue.Count > 0)
               {
                  task = tileLoadQueue.Dequeue();
               }
            }

            if(task.HasValue)
            {
               try
               {
                  #region -- execute --
                  var m = Matrix.GetTileWithReadLock(task.Value.Zoom, task.Value.Pos);

                  if(m == null || m.Overlays.Count == 0)
                  {
                     Debug.WriteLine(ctid + " - Fill empty TileMatrix: " + task);

                     Tile t = new Tile(task.Value.Zoom, task.Value.Pos);
                     var layers = GMaps.Instance.GetAllLayersOfType(MapType);

                     foreach(MapType tl in layers)
                     {
                        int retry = 0;
                        do
                        {
                           PureImage img;
                           Exception ex;

                           // tile number inversion(BottomLeft -> TopLeft) for pergo maps
                           if(tl == MapType.PergoTurkeyMap)
                           {
                              img = GMaps.Instance.GetImageFrom(tl, new GPoint(task.Value.Pos.X, maxOfTiles.Height - task.Value.Pos.Y), task.Value.Zoom, out ex);
                           }
                           else // ok
                           {
                              img = GMaps.Instance.GetImageFrom(tl, task.Value.Pos, task.Value.Zoom, out ex);
                           }

                           if(img != null)
                           {
                              lock(t.Overlays)
                              {
                                 t.Overlays.Add(img);
                              }
                              break;
                           }
                           else
                           {
                              if(ex != null)
                              {
                                 lock(FailedLoads)
                                 {
                                    if(!FailedLoads.ContainsKey(task.Value))
                                    {
                                       FailedLoads.Add(task.Value, ex);

                                       if(OnEmptyTileError != null)
                                       {
                                          if(!RaiseEmptyTileError)
                                          {
                                             RaiseEmptyTileError = true;
                                             OnEmptyTileError(task.Value.Zoom, task.Value.Pos);
                                          }
                                       }
                                    }
                                 }
                              }

                              if(RetryLoadTile > 0)
                              {
                                 Debug.WriteLine(ctid + " - ProcessLoadTask: " + task + " -> empty tile, retry " + retry);
                                 {
                                    Thread.Sleep(1111);
                                 }
                              }
                           }
                        }
                        while(++retry < RetryLoadTile);
                     }

                     if(t.Overlays.Count > 0)
                     {
                        Matrix.SetTile(t);
                     }
                     else
                     {
                        t.Clear();
                        t = null;
                     }

                     layers = null;
                  }
                  #endregion
               }
               catch(Exception ex)
               {
                  Debug.WriteLine(ctid + " - ProcessLoadTask: " + ex.ToString());
               }
               finally
               {
                  lock(LastInvalidationLock)
                  {
                     invalidate = ((DateTime.Now - LastInvalidation).TotalMilliseconds > 111);
                     if(invalidate)
                     {
                        LastInvalidation = DateTime.Now;
                     }
                  }

                  if(invalidate)
                  {
                     if(OnNeedInvalidation != null)
                     {
                        OnNeedInvalidation();
                     }
                  }
#if DEBUG
                  //else
                  //{
                  //   lock(LastInvalidationLock)
                  //   {
                  //      Debug.WriteLine(ctid + " - SkipInvalidation, Delta: " + (DateTime.Now - LastInvalidation).TotalMilliseconds + "ms");
                  //   }
                  //}
#endif
               }
            }
         }

#if !PocketPC
         lock(tileLoadQueue)
         {
            Debug.WriteLine("Quit - " + ct.Name);
            GThreadPool.Remove(ct);
         }
#endif
      }

      /// <summary>
      /// updates map bounds
      /// </summary>
      void UpdateBounds()
      {
         if(MapType == NET.MapType.None)
         {
            return;
         }

         lock(tileLoadQueue)
         {
            tileDrawingListLock.AcquireWriterLock();
            try
            {
               #region -- find tiles around --
               tileDrawingList.Clear();

               for(int i = -sizeOfMapArea.Width; i <= sizeOfMapArea.Width; i++)
               {
                  for(int j = -sizeOfMapArea.Height; j <= sizeOfMapArea.Height; j++)
                  {
                     GPoint p = centerTileXY;
                     p.X += i;
                     p.Y += j;

#if ContinuesMap
               // ----------------------------
               if(p.X < minOfTiles.Width)
               {
                  p.X += (maxOfTiles.Width + 1);
               }

               if(p.X > maxOfTiles.Width)
               {
                  p.X -= (maxOfTiles.Width + 1);
               }
               // ----------------------------
#endif

                     if(p.X >= minOfTiles.Width && p.Y >= minOfTiles.Height && p.X <= maxOfTiles.Width && p.Y <= maxOfTiles.Height)
                     {
                        if(!tileDrawingList.Contains(p))
                        {
                           tileDrawingList.Add(p);
                        }
                     }
                  }
               }

               if(GMaps.Instance.ShuffleTilesOnLoad)
               {
                  Stuff.Shuffle<GPoint>(tileDrawingList);
               }
               #endregion

               foreach(GPoint p in tileDrawingList)
               {
                  LoadTask task = new LoadTask(p, Zoom);
                  {
                     if(!tileLoadQueue.Contains(task))
                     {
                        tileLoadQueue.Enqueue(task);
                     }
                  }
               }
            }
            finally
            {
               tileDrawingListLock.ReleaseWriterLock();
            }

            #region -- starts loader threads if needed --
#if !PocketPC
            while(GThreadPool.Count < GThreadPoolSize)
#else
            while(GThreadPool.Count < GThreadPoolSize)
#endif
            {
               Thread t = new Thread(new ThreadStart(ProcessLoadTask));
               {
                  t.Name = "GMap.NET TileLoader: " + GThreadPool.Count;
                  t.IsBackground = true;
                  t.Priority = ThreadPriority.BelowNormal;
               }
               GThreadPool.Add(t);

               Debug.WriteLine("add " + t.Name + " to GThreadPool");

               t.Start();
            }
            #endregion

            lock(LastTileLoadStartEndLock)
            {
               LastTileLoadStart = DateTime.Now;
               Debug.WriteLine("OnTileLoadStart - at zoom " + Zoom + ", time: " + LastTileLoadStart.TimeOfDay);
            }

            loadWaitCount = 0;

            Monitor.PulseAll(tileLoadQueue);
         }

         if(OnTileLoadStart != null)
         {
            OnTileLoadStart();
         }
      }

      /// <summary>
      /// updates ground resolution info
      /// </summary>
      void UpdateGroundResolution()
      {
         double rez = Projection.GetGroundResolution(Zoom, zoomPosition.Lat);
         pxRes100m = (int)(100.0 / rez); // 100 meters
         pxRes1000m = (int)(1000.0 / rez); // 1km  
         pxRes10km = (int)(10000.0 / rez); // 10km
         pxRes100km = (int)(100000.0 / rez); // 100km
         pxRes1000km = (int)(1000000.0 / rez); // 1000km
         pxRes5000km = (int)(5000000.0 / rez); // 5000km
      }

      internal void RaiseMapTypeChangedEvent()
      {
         if(OnMapTypeChanged != null)
         {
            OnMapTypeChanged(mapType);
         }
      }
   }
}
