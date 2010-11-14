
namespace GMap.NET.WindowsForms
{
   using System;
   using System.ComponentModel;
   using System.Drawing;
   using System.Drawing.Drawing2D;
   using System.Drawing.Imaging;
   using System.IO;
   using System.Threading;
   using System.Windows.Forms;
   using GMap.NET;
   using GMap.NET.Internals;
   using GMap.NET.ObjectModel;
   using System.Diagnostics;
   using System.Drawing.Text;
   using System.Runtime.Serialization.Formatters.Binary;

   /// <summary>
   /// GMap.NET control for Windows Forms
   /// </summary>   
   public partial class GMapControl : UserControl, Interface
   {
      /// <summary>
      /// occurs when clicked on marker
      /// </summary>
      public event MarkerClick OnMarkerClick;

      /// <summary>
      /// occurs on mouse enters marker area
      /// </summary>
      public event MarkerEnter OnMarkerEnter;

      /// <summary>
      /// occurs on mouse leaves marker area
      /// </summary>
      public event MarkerLeave OnMarkerLeave;

      /// <summary>
      /// list of overlays, should be thread safe
      /// </summary>
      public readonly ObservableCollectionThreadSafe<GMapOverlay> Overlays = new ObservableCollectionThreadSafe<GMapOverlay>();

      /// <summary>
      /// max zoom
      /// </summary>         
      [Category("GMap.NET")]
      [Description("maximum zoom level of map")]
      public int MaxZoom
      {
         get
         {
            return Core.maxZoom;
         }
         set
         {
            Core.maxZoom = value;
         }
      }

      /// <summary>
      /// min zoom
      /// </summary>      
      [Category("GMap.NET")]
      [Description("minimum zoom level of map")]
      public int MinZoom
      {
         get
         {
            return Core.minZoom;
         }
         set
         {
            Core.minZoom = value;
         }
      }

      /// <summary>
      /// map zooming type for mouse wheel
      /// </summary>
      [Category("GMap.NET")]
      [Description("map zooming type for mouse wheel")]
      public MouseWheelZoomType MouseWheelZoomType
      {
         get
         {
            return Core.MouseWheelZoomType;
         }
         set
         {
            Core.MouseWheelZoomType = value;
         }
      }

      /// <summary>
      /// text on empty tiles
      /// </summary>
      public string EmptyTileText = "We are sorry, but we don't\nhave imagery at this zoom\nlevel for this region.";

      /// <summary>
      /// pen for empty tile borders
      /// </summary>
#if !PocketPC
      public Pen EmptyTileBorders = new Pen(Brushes.White, 1);
#else
      public Pen EmptyTileBorders = new Pen(Color.White, 1);
#endif

      /// <summary>
      /// pen for scale info
      /// </summary>
#if !PocketPC
      public Pen ScalePen = new Pen(Brushes.Blue, 1);
#else
      public Pen ScalePen = new Pen(Color.Blue, 1);
#endif

#if !PocketPC
      /// <summary>
      /// area selection pen
      /// </summary>
      public Pen SelectionPen = new Pen(Brushes.Blue, 2);

      /// <summary>
      /// background of selected area
      /// </summary>
      public Brush SelectedAreaFill = new SolidBrush(Color.FromArgb(33, Color.RoyalBlue));
#endif

      /// <summary>
      /// pen for empty tile background
      /// </summary>
#if !PocketPC
      public Brush EmptytileBrush = Brushes.Navy;
#else
      public Brush EmptytileBrush = new SolidBrush(Color.Navy);
#endif

#if PocketPC
      public Brush TileGridLinesTextBrush = new SolidBrush(Color.Red);
      public Brush TileGridMissingTextBrush = new SolidBrush(Color.White);
      public Brush CopyrightBrush = new SolidBrush(Color.Navy);
#endif

      /// <summary>
      /// show map scale info
      /// </summary>
      public bool MapScaleInfoEnabled = false;

      /// <summary>
      /// retry count to get tile 
      /// </summary>
      [Browsable(false)]
      public int RetryLoadTile
      {
         get
         {
            return Core.RetryLoadTile;
         }
         set
         {
            Core.RetryLoadTile = value;
         }
      }

      /// <summary>
      /// how many levels of tiles are staying decompresed in memory
      /// </summary>
      [Browsable(false)]
      public int LevelsKeepInMemmory
      {
         get
         {
            return Core.LevelsKeepInMemmory;
         }

         set
         {
            Core.LevelsKeepInMemmory = value;
         }
      }

      /// <summary>
      /// map dragg button
      /// </summary>
      [Category("GMap.NET")]
      public MouseButtons DragButton = MouseButtons.Right;

      private bool showTileGridLines = false;

      /// <summary>
      /// shows tile gridlines
      /// </summary>
      [Category("GMap.NET")]
      [Description("shows tile gridlines")]
      public bool ShowTileGridLines
      {
         get
         {
            return showTileGridLines;
         }
         set
         {
            showTileGridLines = value;
            Invalidate();
         }
      }

      /// <summary>
      /// current selected area in map
      /// </summary>
      private RectLatLng selectedArea;

      [Browsable(false)]
      public RectLatLng SelectedArea
      {
         get
         {
            return selectedArea;
         }
         set
         {
            selectedArea = value;

            if(Core.IsStarted)
            {
               Invalidate();
            }
         }
      }

      /// <summary>
      /// map boundaries
      /// </summary>
      public RectLatLng? BoundsOfMap = null;

      /// <summary>
      /// enables integrated DoubleBuffer for best performance
      /// if using a lot objets on map or running on windows mobile
      /// </summary>
#if !PocketPC
      public bool ForceDoubleBuffer = false;
#else
      readonly bool ForceDoubleBuffer = true;
#endif

      /// <summary>
      /// stops immediate marker/route/polygon invalidations;
      /// call Refresh to perform single refresh and reset invalidation state
      /// </summary>
      public bool HoldInvalidation = false;

      /// <summary>
      /// call this to stop HoldInvalidation and perform single refresh 
      /// </summary>
      public override void Refresh()
      {
         if(HoldInvalidation)
         {
            HoldInvalidation = false;
         }
         base.Refresh();
      }

#if !PocketPC
      private bool _GrayScale = false;

      [Category("GMap.NET")]
      public bool GrayScaleMode
      {
         get
         {
            return _GrayScale;
         }
         set
         {
            _GrayScale = value;
            if(GMaps.Instance.ImageProxy != null && GMaps.Instance.ImageProxy is WindowsFormsImageProxy)
            {
               (GMaps.Instance.ImageProxy as WindowsFormsImageProxy).GrayScale = value;
               if(Core.IsStarted)
               {
                  ReloadMap();
               }
            }
         }
      }
#endif

      // internal stuff
      internal readonly Core Core = new Core();
      internal readonly Font CopyrightFont = new Font(FontFamily.GenericSansSerif, 7, FontStyle.Regular);
#if !PocketPC
      internal readonly Font MissingDataFont = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Bold);
#else
      internal readonly Font MissingDataFont = new Font(FontFamily.GenericSansSerif, 8, FontStyle.Regular);
#endif
      Font ScaleFont = new Font(FontFamily.GenericSansSerif, 5, FontStyle.Italic);
      internal readonly StringFormat CenterFormat = new StringFormat();
      internal readonly StringFormat BottomFormat = new StringFormat();
#if !PocketPC
      readonly ImageAttributes TileFlipXYAttributes = new ImageAttributes();
#endif
      double zoomReal;
      Bitmap backBuffer;
      Graphics gxOff;

#if !DESIGN
      /// <summary>
      /// construct
      /// </summary>
      public GMapControl()
      {
#if !PocketPC
         if(!DesignModeInConstruct && !IsDesignerHosted)
#endif
         {
            WindowsFormsImageProxy wimg = new WindowsFormsImageProxy();

#if !PocketPC
            wimg.GrayScale = this.GrayScaleMode;

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.Opaque, true);
            ResizeRedraw = true;

            TileFlipXYAttributes.SetWrapMode(WrapMode.TileFlipXY);
#endif
            GMaps.Instance.ImageProxy = wimg;

            // to know when to invalidate
            Core.OnNeedInvalidation += new NeedInvalidation(ThreadSafeInvalidation);
            Core.SystemType = "WindowsForms";

            RenderMode = RenderMode.GDI_PLUS;
            Core.currentRegion = new GRect(-50, -50, Size.Width + 100, Size.Height + 100);

            CenterFormat.Alignment = StringAlignment.Center;
            CenterFormat.LineAlignment = StringAlignment.Center;

            BottomFormat.Alignment = StringAlignment.Center;

#if !PocketPC
            BottomFormat.LineAlignment = StringAlignment.Far;
#else
            throw new Exception("this version isn't working, use http://greatmaps.codeplex.com/SourceControl/changeset/changes/22b93afd000c");
#endif

            if(GMaps.Instance.IsRunningOnMono)
            {
               // no imports to move pointer
               MouseWheelZoomType = GMap.NET.MouseWheelZoomType.MousePositionWithoutCenter;
            }

            Overlays.CollectionChanged += new NotifyCollectionChangedEventHandler(Overlays_CollectionChanged);
         }
      }

      void Overlays_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
      {
         if(e.NewItems != null)
         {
            foreach(GMapOverlay obj in e.NewItems)
            {
               if(obj != null)
               {
                  obj.Control = this;
               }
            }
         }
      }
#endif

      /// <summary>
      /// update objects when map is zoomed/rotated
      /// </summary>
      internal void ForceUpdateOverlays()
      {
         try
         {
            HoldInvalidation = true;

            foreach(GMapOverlay o in Overlays)
            {
               if(o.IsVisibile)
               {
                  o.ForceUpdate();
               }
            }
         }
         finally
         {
            Refresh();
         }
      }

      /// <summary>
      /// thread safe invalidation
      /// </summary>
      internal void ThreadSafeInvalidation()
      {
         if(InvokeRequired)
         {
            MethodInvoker m = delegate
            {
               Invalidate();
            };
            try
            {
#if !PocketPC
               this.BeginInvoke(m);
#else
               this.Invoke(m);
#endif
            }
            catch
            {
            }
         }
         else
         {
            Invalidate();
         }
      }

      /// <summary>
      /// render map in GDI+
      /// </summary>
      /// <param name="g"></param>
      void DrawMapDesktop(Graphics g)
      {
         if(MapType == NET.MapType.None)
         {
            return;
         }

         Core.Matrix.EnterReadLock();
         Core.tileDrawingListLock.AcquireReaderLock();
         try
         {
            foreach(var tilePoint in Core.tileDrawingList)
            {
#if ContinuesMap
               //-----
               GPoint tileToDraw = Core.tilePoint;  
               if(tileToDraw.X < Core.minOfTiles.Width)
               {
                  tileToDraw.X += (Core.maxOfTiles.Width + 1);
               }
               if(tileToDraw.X > Core.maxOfTiles.Width)
               {
                  tileToDraw.X -= (Core.maxOfTiles.Width + 1);
               }
               //-----
#endif
               {
                  Core.tileRect.X = tilePoint.X * Core.tileRect.Width;
                  Core.tileRect.Y = tilePoint.Y * Core.tileRect.Height;

                  //Core.tileRect.X = tilePoint.X * Core.tileRect.Width - Core.zoomPositionPixel.X;
                  //Core.tileRect.Y = tilePoint.Y * Core.tileRect.Height - Core.zoomPositionPixel.Y;

                  if(Core.viewRectPixelInflated.IntersectsWith(Core.tileRect) || IsRotated)
                  {
                     bool found = false;
#if !ContinuesMap

                     Tile t = Core.Matrix.GetTileWithNoLock(Core.Zoom, tilePoint);
#else
                     Tile t = Core.Matrix.GetTileWithNoLock(Core.Zoom, tileToDraw);
#endif
                     if(t != null)
                     {
                        // render tile
                        lock(t.Overlays)
                        {
                           foreach(WindowsFormsImage img in t.Overlays)
                           {
                              if(img != null && img.Img != null)
                              {
                                 if(!found)
                                    found = true;
#if !PocketPC

                                 g.DrawImage(img.Img, Core.tileRect.X, Core.tileRect.Y, Core.tileRectBearing.Width, Core.tileRectBearing.Height);
#else
                                 g.DrawImage(img.Img, Core.tileRect.X, Core.tileRect.Y);
#endif
                              }
                           }
                        }
                     }
#if !PocketPC
                     else // testing smooth zooming
                     {
                        int ZoomOffset = 0;
                        Tile ParentTile = null;
                        int Ix = 0;

                        while(ParentTile == null && (Core.Zoom - ZoomOffset) >= 1 && ZoomOffset <= LevelsKeepInMemmory)
                        {
                           Ix = (int) Math.Pow(2, ++ZoomOffset);
                           ParentTile = Core.Matrix.GetTileWithNoLock(Core.Zoom - ZoomOffset, new GPoint((int) (tilePoint.X / Ix), (int) (tilePoint.Y / Ix)));
                        }

                        if(ParentTile != null)
                        {
                           int Xoff = Math.Abs(tilePoint.X - (ParentTile.Pos.X * Ix));
                           int Yoff = Math.Abs(tilePoint.Y - (ParentTile.Pos.Y * Ix));

                           // render tile 
                           lock(ParentTile.Overlays)
                           {
                              foreach(WindowsFormsImage img in ParentTile.Overlays)
                              {
                                 if(img != null && img.Img != null)
                                 {
                                    if(!found)
                                       found = true;

                                    System.Drawing.RectangleF srcRect = new System.Drawing.RectangleF((float) (Xoff * (img.Img.Width / Ix)), (float) (Yoff * (img.Img.Height / Ix)), (img.Img.Width / Ix), (img.Img.Height / Ix));
                                    System.Drawing.Rectangle dst = new System.Drawing.Rectangle(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height);

                                    g.DrawImage(img.Img, dst, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, TileFlipXYAttributes);
                                    g.FillRectangle(SelectedAreaFill, dst);
                                 }
                              }
                           }
                        }
                     }
#endif
                     // add text if tile is missing
                     if(!found)
                     {
                        lock(Core.FailedLoads)
                        {
                           var lt = new LoadTask(tilePoint, Core.Zoom);
                           if(Core.FailedLoads.ContainsKey(lt))
                           {
                              var ex = Core.FailedLoads[lt];
#if !PocketPC
                              g.FillRectangle(EmptytileBrush, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height));

                              g.DrawString("Exception: " + ex.Message, MissingDataFont, Brushes.Red, new RectangleF(Core.tileRect.X + 11, Core.tileRect.Y + 11, Core.tileRect.Width - 11, Core.tileRect.Height - 11));

                              g.DrawString(EmptyTileText, MissingDataFont, Brushes.Blue, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height), CenterFormat);

#else
                              g.FillRectangle(EmptytileBrush, new System.Drawing.Rectangle(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height));

                              g.DrawString("Exception: " + ex.Message, MissingDataFont, TileGridMissingTextBrush, new RectangleF(Core.tileRect.X + 11, Core.tileRect.Y + 11, Core.tileRect.Width - 11, Core.tileRect.Height - 11));

                              g.DrawString(EmptyTileText, MissingDataFont, TileGridMissingTextBrush, new RectangleF(Core.tileRect.X, Core.tileRect.Y + Core.tileRect.Width/2 + (ShowTileGridLines? 11 : -22), Core.tileRect.Width, Core.tileRect.Height), BottomFormat);
#endif

                              g.DrawRectangle(EmptyTileBorders, Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height);
                           }
                        }
                     }

                     if(ShowTileGridLines)
                     {
                        g.DrawRectangle(EmptyTileBorders, Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height);
                        {
#if !PocketPC
                           g.DrawString((tilePoint == Core.centerTileXY ? "CENTER: " : "TILE: ") + tilePoint, MissingDataFont, Brushes.Red, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height), CenterFormat);
#else
                           g.DrawString((tilePoint == Core.centerTileXYLocation? "" :"TILE: ") + tilePoint, MissingDataFont, TileGridLinesTextBrush, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height), CenterFormat);
#endif
                        }
                     }
                  }
               }
            }
         }
         finally
         {
            Core.tileDrawingListLock.ReleaseReaderLock();
            Core.Matrix.LeaveReadLock();
         }
      }

      void DrawMapMobile(Graphics g)
      {
         if(MapType == NET.MapType.None)
         {
            return;
         }

         Core.Matrix.EnterReadLock();
         Core.tileDrawingListLock.AcquireReaderLock();
         try
         {
            foreach(var tilePoint in Core.tileDrawingList)
            {
#if ContinuesMap
               //-----
               GPoint tileToDraw = Core.tilePoint;  
               if(tileToDraw.X < Core.minOfTiles.Width)
               {
                  tileToDraw.X += (Core.maxOfTiles.Width + 1);
               }
               if(tileToDraw.X > Core.maxOfTiles.Width)
               {
                  tileToDraw.X -= (Core.maxOfTiles.Width + 1);
               }
               //-----
#endif
               {
                  Core.tileRect.X = tilePoint.X * Core.tileRect.Width;
                  Core.tileRect.Y = tilePoint.Y * Core.tileRect.Height;
                  Core.tileRect.Offset(Core.renderOffset);

                  if(Core.currentRegion.IntersectsWith(Core.tileRect) || IsRotated)
                  {
                     bool found = false;
#if !ContinuesMap

                     Tile t = Core.Matrix.GetTileWithNoLock(Core.Zoom, tilePoint);
#else
                     Tile t = Core.Matrix.GetTileWithNoLock(Core.Zoom, tileToDraw);
#endif
                     if(t != null)
                     {
                        // render tile
                        lock(t.Overlays)
                        {
                           foreach(WindowsFormsImage img in t.Overlays)
                           {
                              if(img != null && img.Img != null)
                              {
                                 if(!found)
                                    found = true;
#if !PocketPC

                                 g.DrawImage(img.Img, Core.tileRect.X, Core.tileRect.Y, Core.tileRectBearing.Width, Core.tileRectBearing.Height);
#else
                                 g.DrawImage(img.Img, Core.tileRect.X, Core.tileRect.Y);
#endif
                              }
                           }
                        }
                     }
#if !PocketPC
                     else // testing smooth zooming
                     {
                        int ZoomOffset = 0;
                        Tile ParentTile = null;
                        int Ix = 0;

                        while(ParentTile == null && (Core.Zoom - ZoomOffset) >= 1 && ZoomOffset <= LevelsKeepInMemmory)
                        {
                           Ix = (int) Math.Pow(2, ++ZoomOffset);
                           ParentTile = Core.Matrix.GetTileWithNoLock(Core.Zoom - ZoomOffset, new GPoint((int) (tilePoint.X / Ix), (int) (tilePoint.Y / Ix)));
                        }

                        if(ParentTile != null)
                        {
                           int Xoff = Math.Abs(tilePoint.X - (ParentTile.Pos.X * Ix));
                           int Yoff = Math.Abs(tilePoint.Y - (ParentTile.Pos.Y * Ix));

                           // render tile 
                           lock(ParentTile.Overlays)
                           {
                              foreach(WindowsFormsImage img in ParentTile.Overlays)
                              {
                                 if(img != null && img.Img != null)
                                 {
                                    if(!found)
                                       found = true;

                                    System.Drawing.RectangleF srcRect = new System.Drawing.RectangleF((float) (Xoff * (img.Img.Width / Ix)), (float) (Yoff * (img.Img.Height / Ix)), (img.Img.Width / Ix), (img.Img.Height / Ix));
                                    System.Drawing.Rectangle dst = new System.Drawing.Rectangle(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height);

                                    g.DrawImage(img.Img, dst, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, TileFlipXYAttributes);
                                    g.FillRectangle(SelectedAreaFill, dst);
                                 }
                              }
                           }
                        }
                     }
#endif
                     // add text if tile is missing
                     if(!found)
                     {
                        lock(Core.FailedLoads)
                        {
                           var lt = new LoadTask(tilePoint, Core.Zoom);
                           if(Core.FailedLoads.ContainsKey(lt))
                           {
                              var ex = Core.FailedLoads[lt];
#if !PocketPC
                              g.FillRectangle(EmptytileBrush, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height));

                              g.DrawString("Exception: " + ex.Message, MissingDataFont, Brushes.Red, new RectangleF(Core.tileRect.X + 11, Core.tileRect.Y + 11, Core.tileRect.Width - 11, Core.tileRect.Height - 11));

                              g.DrawString(EmptyTileText, MissingDataFont, Brushes.Blue, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height), CenterFormat);

#else
                              g.FillRectangle(EmptytileBrush, new System.Drawing.Rectangle(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height));

                              g.DrawString("Exception: " + ex.Message, MissingDataFont, TileGridMissingTextBrush, new RectangleF(Core.tileRect.X + 11, Core.tileRect.Y + 11, Core.tileRect.Width - 11, Core.tileRect.Height - 11));

                              g.DrawString(EmptyTileText, MissingDataFont, TileGridMissingTextBrush, new RectangleF(Core.tileRect.X, Core.tileRect.Y + Core.tileRect.Width/2 + (ShowTileGridLines? 11 : -22), Core.tileRect.Width, Core.tileRect.Height), BottomFormat);
#endif

                              g.DrawRectangle(EmptyTileBorders, Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height);
                           }
                        }
                     }

                     if(ShowTileGridLines)
                     {
                        g.DrawRectangle(EmptyTileBorders, Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height);
                        {
#if !PocketPC
                           g.DrawString((tilePoint == Core.centerTileXY ? "CENTER: " : "TILE: ") + tilePoint, MissingDataFont, Brushes.Red, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height), CenterFormat);
#else
                           g.DrawString((tilePoint == Core.centerTileXYLocation? "" :"TILE: ") + tilePoint, MissingDataFont, TileGridLinesTextBrush, new RectangleF(Core.tileRect.X, Core.tileRect.Y, Core.tileRect.Width, Core.tileRect.Height), CenterFormat);
#endif
                        }
                     }
                  }
               }
            }
         }
         finally
         {
            Core.tileDrawingListLock.ReleaseReaderLock();
            Core.Matrix.LeaveReadLock();
         }
      }

      /// <summary>
      /// updates markers local position
      /// </summary>
      /// <param name="marker"></param>
      public void UpdateMarkerLocalPosition(GMapMarker marker)
      {
         GPoint p = FromLatLngToLocal(marker.Position);
         {
            marker.LocalOrigin = new System.Drawing.Point(p.X, p.Y);
         }
      }

      /// <summary>
      /// updates routes local position
      /// </summary>
      /// <param name="route"></param>
      public void UpdateRouteLocalPosition(GMapRoute route)
      {
         route.LocalPoints.Clear();

         foreach(GMap.NET.PointLatLng pg in route.Points)
         {
            GPoint p = Projection.FromLatLngToPixel(pg, Core.Zoom, true);

            if(IsRotated)
            {
               System.Drawing.Point[] tt = new System.Drawing.Point[] { new System.Drawing.Point(p.X, p.Y) };
               rotationMatrix.TransformPoints(tt);
               var f = tt[0];

               p.X = f.X;
               p.Y = f.Y;
            }

            route.LocalPoints.Add(p);
         }
      }

      /// <summary>
      /// updates polygons local position
      /// </summary>
      /// <param name="polygon"></param>
      public void UpdatePolygonLocalPosition(GMapPolygon polygon)
      {
         polygon.LocalPoints.Clear();

         foreach(GMap.NET.PointLatLng pg in polygon.Points)
         {
            GPoint p = Projection.FromLatLngToPixel(pg, Core.Zoom, true);

            if(IsRotated)
            {
               System.Drawing.Point[] tt = new System.Drawing.Point[] { new System.Drawing.Point(p.X, p.Y) };
               rotationMatrix.TransformPoints(tt);
               var f = tt[0];

               p.X = f.X;
               p.Y = f.Y;
            }

            polygon.LocalPoints.Add(p);
         }
      }

      /// <summary>
      /// sets zoom to max to fit rect
      /// </summary>
      /// <param name="rect"></param>
      /// <returns></returns>
      public bool SetZoomToFitRect(RectLatLng rect)
      {
         int maxZoom = Core.GetMaxZoomToFitRect(rect);
         if(maxZoom > 0)
         {
            PointLatLng center = new PointLatLng(rect.Lat - (rect.HeightLat / 2), rect.Lng + (rect.WidthLng / 2));
            Position = center;

            if(maxZoom > MaxZoom)
            {
               maxZoom = MaxZoom;
            }

            if((int) Zoom != maxZoom)
            {
               Zoom = maxZoom;
            }

            return true;
         }
         return false;
      }

      /// <summary>
      /// sets to max zoom to fit all markers and centers them in map
      /// </summary>
      /// <param name="overlayId">overlay id or null to check all</param>
      /// <returns></returns>
      public bool ZoomAndCenterMarkers(string overlayId)
      {
         RectLatLng? rect = GetRectOfAllMarkers(overlayId);
         if(rect.HasValue)
         {
            return SetZoomToFitRect(rect.Value);
         }

         return false;
      }

      /// <summary>
      /// zooms and centers all route
      /// </summary>
      /// <param name="overlayId">overlay id or null to check all</param>
      /// <returns></returns>
      public bool ZoomAndCenterRoutes(string overlayId)
      {
         RectLatLng? rect = GetRectOfAllRoutes(overlayId);
         if(rect.HasValue)
         {
            return SetZoomToFitRect(rect.Value);
         }

         return false;
      }

      /// <summary>
      /// zooms and centers route 
      /// </summary>
      /// <param name="route"></param>
      /// <returns></returns>
      public bool ZoomAndCenterRoute(MapRoute route)
      {
         RectLatLng? rect = GetRectOfRoute(route);
         if(rect.HasValue)
         {
            return SetZoomToFitRect(rect.Value);
         }

         return false;
      }

      /// <summary>
      /// gets rectangle with all objects inside
      /// </summary>
      /// <param name="overlayId">overlay id or null to check all</param>
      /// <returns></returns>
      public RectLatLng? GetRectOfAllMarkers(string overlayId)
      {
         RectLatLng? ret = null;

         double left = double.MaxValue;
         double top = double.MinValue;
         double right = double.MinValue;
         double bottom = double.MaxValue;

         foreach(GMapOverlay o in Overlays)
         {
            if(overlayId == null || o.Id == overlayId)
            {
               if(o.IsVisibile && o.Markers.Count > 0)
               {
                  foreach(GMapMarker m in o.Markers)
                  {
                     if(m.IsVisible)
                     {
                        // left
                        if(m.Position.Lng < left)
                        {
                           left = m.Position.Lng;
                        }

                        // top
                        if(m.Position.Lat > top)
                        {
                           top = m.Position.Lat;
                        }

                        // right
                        if(m.Position.Lng > right)
                        {
                           right = m.Position.Lng;
                        }

                        // bottom
                        if(m.Position.Lat < bottom)
                        {
                           bottom = m.Position.Lat;
                        }
                     }
                  }
               }
            }
         }

         if(left != double.MaxValue && right != double.MinValue && top != double.MinValue && bottom != double.MaxValue)
         {
            ret = RectLatLng.FromLTRB(left, top, right, bottom);
         }

         return ret;
      }

      /// <summary>
      /// gets rectangle with all objects inside
      /// </summary>
      /// <param name="overlayId">overlay id or null to check all</param>
      /// <returns></returns>
      public RectLatLng? GetRectOfAllRoutes(string overlayId)
      {
         RectLatLng? ret = null;

         double left = double.MaxValue;
         double top = double.MinValue;
         double right = double.MinValue;
         double bottom = double.MaxValue;

         foreach(GMapOverlay o in Overlays)
         {
            if(overlayId == null || o.Id == overlayId)
            {
               if(o.IsVisibile && o.Routes.Count > 0)
               {
                  foreach(MapRoute route in o.Routes)
                  {
                     if(route.From.HasValue && route.To.HasValue)
                     {
                        foreach(PointLatLng p in route.Points)
                        {
                           // left
                           if(p.Lng < left)
                           {
                              left = p.Lng;
                           }

                           // top
                           if(p.Lat > top)
                           {
                              top = p.Lat;
                           }

                           // right
                           if(p.Lng > right)
                           {
                              right = p.Lng;
                           }

                           // bottom
                           if(p.Lat < bottom)
                           {
                              bottom = p.Lat;
                           }
                        }
                     }
                  }
               }
            }
         }

         if(left != double.MaxValue && right != double.MinValue && top != double.MinValue && bottom != double.MaxValue)
         {
            ret = RectLatLng.FromLTRB(left, top, right, bottom);
         }

         return ret;
      }

      /// <summary>
      /// gets rect of route
      /// </summary>
      /// <param name="route"></param>
      /// <returns></returns>
      public RectLatLng? GetRectOfRoute(MapRoute route)
      {
         RectLatLng? ret = null;

         double left = double.MaxValue;
         double top = double.MinValue;
         double right = double.MinValue;
         double bottom = double.MaxValue;

         if(route.From.HasValue && route.To.HasValue)
         {
            foreach(PointLatLng p in route.Points)
            {
               // left
               if(p.Lng < left)
               {
                  left = p.Lng;
               }

               // top
               if(p.Lat > top)
               {
                  top = p.Lat;
               }

               // right
               if(p.Lng > right)
               {
                  right = p.Lng;
               }

               // bottom
               if(p.Lat < bottom)
               {
                  bottom = p.Lat;
               }
            }
            ret = RectLatLng.FromLTRB(left, top, right, bottom);
         }
         return ret;
      }

#if !PocketPC
      /// <summary>
      /// gets image of the current view
      /// </summary>
      /// <returns></returns>
      public Image ToImage()
      {
         Image ret = null;
         try
         {
            using(Bitmap bitmap = new Bitmap(Width, Height))
            {
               using(Graphics g = Graphics.FromImage(bitmap))
               {
                  using(Graphics gg = this.CreateGraphics())
                  {
#if !PocketPC
                     g.CopyFromScreen(PointToScreen(new System.Drawing.Point()).X, PointToScreen(new System.Drawing.Point()).Y, 0, 0, new System.Drawing.Size(Width, Height));
#else
                     throw new NotImplementedException("Not implemeted for PocketPC");
#endif
                  }
               }

               // Convert the Image to a png
               using(MemoryStream ms = new MemoryStream())
               {
                  bitmap.Save(ms, ImageFormat.Png);
#if !PocketPC
                  ret = Image.FromStream(ms);
#else
                  throw new NotImplementedException("Not implemeted for PocketPC");
#endif
               }
            }
         }
         catch
         {
            ret = null;
         }
         return ret;
      }
#endif

      /// <summary>
      /// offset position in pixels
      /// </summary>
      /// <param name="x"></param>
      /// <param name="y"></param>
      public void Offset(int x, int y)
      {
         if(IsHandleCreated)
         {
            // need to fix in rotated mode usinf rotationMatrix
            // ...
            Core.DragOffset(new GPoint(x, y));
         }
      }

      #region UserControl Events

#if !PocketPC
      protected bool DesignModeInConstruct
      {
         get
         {
            return (LicenseManager.UsageMode == LicenseUsageMode.Designtime);
         }
      }

      [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      public bool IsDesignerHosted
      {
         get
         {
            return IsControlDesignerHosted(this);
         }
      }

      public bool IsControlDesignerHosted(Control ctrl)
      {
         if(ctrl != null)
         {
            if(ctrl.Site != null)
            {

               if(ctrl.Site.DesignMode == true)
                  return true;

               else
               {
                  if(IsControlDesignerHosted(ctrl.Parent))
                     return true;

                  else
                     return false;
               }
            }
            else
            {
               if(IsControlDesignerHosted(ctrl.Parent))
                  return true;
               else
                  return false;
            }
         }
         else
            return false;
      }

      protected override void OnLoad(EventArgs e)
      {
         base.OnLoad(e);

         if(!IsDesignerHosted)
         {
            MethodInvoker m = delegate
            {
               Thread.Sleep(222);
               Core.StartSystem();

               ForceUpdateOverlays();
            };
            this.BeginInvoke(m);
         }
      }
#else
      delegate void MethodInvoker();
      bool IsHandleCreated = false;

      protected override void OnHandleCreated(EventArgs e)
      {
         base.OnHandleCreated(e);
         {
            MethodInvoker m = delegate
            {
               Thread.Sleep(222);
               Core.StartSystem();
            };
            this.BeginInvoke(m);
         }
         IsHandleCreated = true;
      }

      protected override void OnPaintBackground(PaintEventArgs e)
      {
         // ...
      }
#endif

      protected override void OnHandleDestroyed(EventArgs e)
      {
         Core.OnMapClose();
         Core.ApplicationExit();

         base.OnHandleDestroyed(e);
      }

      PointLatLng selectionStart;
      PointLatLng selectionEnd;

#if !PocketPC
      float? MapRenderTransform = null;
#endif

      public Color EmptyMapBackground = Color.WhiteSmoke;

      protected override void OnPaint(PaintEventArgs e)
      {
         if(ForceDoubleBuffer)
         {
            if(gxOff != null && backBuffer != null)
            {
               // render white background
               gxOff.Clear(EmptyMapBackground);

#if !PocketPC
               if(MapRenderTransform.HasValue)
               {
                  gxOff.ScaleTransform(MapRenderTransform.Value, MapRenderTransform.Value);
                  {
                     DrawMapDesktop(gxOff);
                  }
                  gxOff.ResetTransform();
               }
               else
#endif
               {
                  //DrawMapMobile(gxOff);

                  gxOff.TranslateTransform(Core.renderOffset.X, Core.renderOffset.Y);
                  DrawMapDesktop(gxOff);
               }

               OnPaintEtc(gxOff);

               e.Graphics.DrawImage(backBuffer, 0, 0);
            }
         }
         else
         {
            e.Graphics.Clear(EmptyMapBackground);

#if !PocketPC
            if(MapRenderTransform.HasValue)
            {
               e.Graphics.ScaleTransform(MapRenderTransform.Value, MapRenderTransform.Value);
               {
                  DrawMapDesktop(e.Graphics);
               }
               e.Graphics.ResetTransform();
            }
            else
#endif
            {
#if DEBUG
#if !PocketPC
               if(VirtualSizeEnabled)
               {
                  e.Graphics.ScaleTransform(0.5f, 0.5f);
                  e.Graphics.TranslateTransform((Width - Width * 0.5f), (Height - Height * 0.5f));
               }
#endif
#endif

               e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
               e.Graphics.PageUnit = GraphicsUnit.Pixel;
               e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
               e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
               e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

               // need a solution to lower high number offsets
               // to remove translate distortions
               e.Graphics.TranslateTransform(Core.renderOffset.X, Core.renderOffset.Y);
               //e.Graphics.TranslateTransform(Core.renderOffset.X + Core.zoomPositionPixel.X, Core.renderOffset.Y - Core.zoomPositionPixel.Y);


               if(IsRotated)
               {
                  e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                  e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                  e.Graphics.TranslateTransform(PositionPixel.X, PositionPixel.Y);
                  e.Graphics.RotateTransform(-Bearing);
                  e.Graphics.TranslateTransform(-PositionPixel.X, -PositionPixel.Y);

                  DrawMapDesktop(e.Graphics);

                  //e.Graphics.ResetTransform();
                  //e.Graphics.TranslateTransform(Core.renderOffset.X, Core.renderOffset.Y);

                  OnPaintEtc(e.Graphics);
               }
               else
               {
                  DrawMapDesktop(e.Graphics);
                  OnPaintEtc(e.Graphics);
               }
            }

#if DEBUG
#if !PocketPC
            if(VirtualSizeEnabled)
            {
               e.Graphics.ScaleTransform(0.5f, 0.5f);
               e.Graphics.TranslateTransform((Width - Width * 0.5f), (Height - Height * 0.5f));

               e.Graphics.DrawRectangle(SelectionPen, 0, 0, Width, Height);
            }
#endif
#endif
         }

         base.OnPaint(e);
      }

      readonly Matrix rotationMatrix = new Matrix();
      readonly Matrix rotationMatrixInvert = new Matrix();

      /// <summary>
      /// updates rotation matrix
      /// </summary>
      void UpdateRotationMatrix()
      {
#if !PocketPC
         PointF center = new PointF(PositionPixel.X, PositionPixel.Y);

         rotationMatrix.Reset();
         rotationMatrix.RotateAt(-Bearing, center);

         rotationMatrixInvert.Reset();
         rotationMatrixInvert.RotateAt(-Bearing, center);
         rotationMatrixInvert.Invert();
#endif
      }

      /// <summary>
      /// returs true if map bearing is not zero
      /// </summary>    
      [Browsable(false)]
      public bool IsRotated
      {
         get
         {
            return Core.IsRotated;
         }
      }

      /// <summary>
      /// bearing for rotation of the map
      /// </summary>
      [Category("GMap.NET")]
      public float Bearing
      {
         get
         {
            return Core.bearing;
         }
         set
         {
            if(Core.bearing != value)
            {
               bool resize = Core.bearing == 0;
               Core.bearing = value;

               UpdateRotationMatrix();

               if(value != 0 && value % 360 != 0)
               {
                  Core.IsRotated = true;

                  if(Core.tileRectBearing.Size == Core.tileRect.Size)
                  {
                     Core.tileRectBearing = Core.tileRect;
                     Core.tileRectBearing.Inflate(1, 1);
                  }
               }
               else
               {
                  Core.IsRotated = false;
                  Core.tileRectBearing = Core.tileRect;
               }

               if(resize)
               {
                  Core.OnMapSizeChanged(Width, Height, true);
               }

               if(!HoldInvalidation && Core.IsStarted)
               {
                  //ForceUpdateOverlays();
                  Invalidate();
               }
            }
         }
      }

      /// <summary>
      /// override, to render something more
      /// </summary>
      /// <param name="g"></param>
      protected virtual void OnPaintEtc(Graphics g)
      {
#if !PocketPC
         g.SmoothingMode = SmoothingMode.HighQuality;
#endif
         foreach(GMapOverlay o in Overlays)
         {
            if(o.IsVisibile)
            {
               o.Render(g);
            }
         }

         g.ResetTransform();

#if !PocketPC
         if(!SelectedArea.IsEmpty)
         {
            GPoint p1 = FromLatLngToLocal(SelectedArea.LocationTopLeft);
            GPoint p2 = FromLatLngToLocal(SelectedArea.LocationRightBottom);

            int x1 = p1.X;
            int y1 = p1.Y;
            int x2 = p2.X;
            int y2 = p2.Y;

            g.DrawRectangle(SelectionPen, x1, y1, x2 - x1, y2 - y1);
            g.FillRectangle(SelectedAreaFill, x1, y1, x2 - x1, y2 - y1);
         }
#endif

         #region -- copyright --

         switch(Core.MapType)
         {
            case MapType.GoogleMap:
            case MapType.GoogleSatellite:
            case MapType.GoogleLabels:
            case MapType.GoogleTerrain:
            case MapType.GoogleHybrid:
            {
#if !PocketPC
               g.DrawString(Core.googleCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.googleCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;

            case MapType.OpenStreetMap:
            case MapType.OpenStreetOsm:
            case MapType.OpenStreetMapSurfer:
            case MapType.OpenStreetMapSurferTerrain:
            case MapType.OpenSeaMapLabels:
            case MapType.OpenSeaMapHybrid:
            {
#if !PocketPC
               g.DrawString(Core.openStreetMapCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.openStreetMapCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;

            case MapType.YahooMap:
            case MapType.YahooSatellite:
            case MapType.YahooLabels:
            case MapType.YahooHybrid:
            {
#if !PocketPC
               g.DrawString(Core.yahooMapCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.yahooMapCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;

            case MapType.BingHybrid:
            case MapType.BingMap:
            case MapType.BingMap_New:
            case MapType.BingSatellite:
            {
#if !PocketPC
               g.DrawString(Core.virtualEarthCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.virtualEarthCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;

            case MapType.ArcGIS_StreetMap_World_2D:
            case MapType.ArcGIS_Imagery_World_2D:
            case MapType.ArcGIS_ShadedRelief_World_2D:
            case MapType.ArcGIS_Topo_US_2D:
            case MapType.ArcGIS_World_Physical_Map:
            case MapType.ArcGIS_World_Shaded_Relief:
            case MapType.ArcGIS_World_Street_Map:
            case MapType.ArcGIS_World_Terrain_Base:
            case MapType.ArcGIS_World_Topo_Map:
            {
#if !PocketPC
               g.DrawString(Core.arcGisCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.arcGisCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;

            case MapType.MapsLT_OrtoFoto:
            case MapType.MapsLT_Map:
            case MapType.MapsLT_Map_Hybrid:
            case MapType.MapsLT_Map_Labels:
            {
#if !PocketPC
               g.DrawString(Core.hnitCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.hnitCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;

            case MapType.PergoTurkeyMap:
            {
#if !PocketPC
               g.DrawString(Core.pergoCopyright, CopyrightFont, Brushes.Navy, 3, Height - CopyrightFont.Height - 5);
#else
               g.DrawString(Core.pergoCopyright, CopyrightFont, CopyrightBrush, 3, Height - CopyrightFont.Size - 15);
#endif
            }
            break;
         }

         #endregion

         #region -- draw scale --
#if !PocketPC
         if(MapScaleInfoEnabled)
         {
            if(Width - 50 > Core.pxRes5000km)
            {
               g.DrawRectangle(ScalePen, 10, 10, Core.pxRes5000km, 10);
               g.DrawString("5000Km", ScaleFont, Brushes.Blue, Core.pxRes5000km + 10, 11);
            }
            if(Width - 50 > Core.pxRes1000km)
            {
               g.DrawRectangle(ScalePen, 10, 10, Core.pxRes1000km, 10);
               g.DrawString("1000Km", ScaleFont, Brushes.Blue, Core.pxRes1000km + 10, 11);
            }
            if(Width - 50 > Core.pxRes100km && Zoom > 2)
            {
               g.DrawRectangle(ScalePen, 10, 10, Core.pxRes100km, 10);
               g.DrawString("100Km", ScaleFont, Brushes.Blue, Core.pxRes100km + 10, 11);
            }
            if(Width - 50 > Core.pxRes10km && Zoom > 5)
            {
               g.DrawRectangle(ScalePen, 10, 10, Core.pxRes10km, 10);
               g.DrawString("10Km", ScaleFont, Brushes.Blue, Core.pxRes10km + 10, 11);
            }
            if(Width - 50 > Core.pxRes1000m && Zoom >= 10)
            {
               g.DrawRectangle(ScalePen, 10, 10, Core.pxRes1000m, 10);
               g.DrawString("1000m", ScaleFont, Brushes.Blue, Core.pxRes1000m + 10, 11);
            }
            if(Width - 50 > Core.pxRes100m && Zoom > 11)
            {
               g.DrawRectangle(ScalePen, 10, 10, Core.pxRes100m, 10);
               g.DrawString("100m", ScaleFont, Brushes.Blue, Core.pxRes100m + 9, 11);
            }
         }
#endif
         #endregion
      }

#if !PocketPC

#if DEBUG
      /// <summary>
      /// shrinks map area, useful just for testing
      /// </summary>
      [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      public bool VirtualSizeEnabled
      {
         get
         {
            return Core.VirtualSizeEnabled;
         }
         set
         {
            Core.VirtualSizeEnabled = value;
         }
      }
#endif

      protected override void OnSizeChanged(EventArgs e)
      {
         base.OnSizeChanged(e);

         if(!IsDesignerHosted && !DesignModeInConstruct)
         {
            if(ForceDoubleBuffer)
            {
               if(backBuffer != null)
               {
                  backBuffer.Dispose();
                  backBuffer = null;
               }
               if(gxOff != null)
               {
                  gxOff.Dispose();
                  gxOff = null;
               }

               backBuffer = new Bitmap(Width, Height);
               gxOff = Graphics.FromImage(backBuffer);
            }

            {
               Core.OnMapSizeChanged(Width, Height, true);
               Core.currentRegion = new GRect(-50, -50, Core.Width + 50, Core.Height + 50);
            }

            if(Visible && IsHandleCreated)
            {
               // keep center on same position
               Core.GoToCurrentPosition();

               if(IsRotated)
               {
                  UpdateRotationMatrix();
               }
            }
         }
      }
#else
#if !DESIGN
      protected override void OnResize(EventArgs e)
      {
         base.OnResize(e);

         if(ForceDoubleBuffer)
         {
            if(backBuffer != null)
            {
               backBuffer.Dispose();
               backBuffer = null;
            }
            if(gxOff != null)
            {
               gxOff.Dispose();
               gxOff = null;
            }

            backBuffer = new Bitmap(Width, Height);
            gxOff = Graphics.FromImage(backBuffer);
         }

         Core.OnMapSizeChanged(Width, Height);

         // 50px outside control
         Core.currentRegion = new GRect(-50, -50, Size.Width+100, Size.Height+100);

         if(Core.IsStarted)
         {
            if(Visible) // && IsHandleCreated
            {
               // keep center on same position
               Core.GoToCurrentPosition();
            }
         }
      }
#endif
#endif

      bool isSelected = false;
      protected override void OnMouseDown(MouseEventArgs e)
      {
         if(!IsMouseOverMarker)
         {
#if !PocketPC
            if(e.Button == DragButton && CanDragMap)
#else
            if(CanDragMap)
#endif
            {
               Core.mouseDown = ApplyRotationInversion(e.X, e.Y);

#if !PocketPC
               this.Cursor = System.Windows.Forms.Cursors.SizeAll;
#endif
               Core.BeginDrag(Core.mouseDown);

               this.Invalidate();
            }
            else if(!isSelected && (Form.ModifierKeys == Keys.Alt || Form.ModifierKeys == Keys.Shift))
            {
               isSelected = true;
               SelectedArea = RectLatLng.Empty;
               selectionEnd = PointLatLng.Zero;
               selectionStart = FromLocalToLatLng(e.X, e.Y);
            }
         }

         base.OnMouseDown(e);
      }

      protected override void OnMouseUp(MouseEventArgs e)
      {
         base.OnMouseUp(e);

         if(isSelected)
         {
            isSelected = false;
         }

         if(Core.IsDragging)
         {
            if(isDragging)
            {
               isDragging = false;
               Debug.WriteLine("IsDragging = " + isDragging);
            }
            Core.EndDrag();

#if !PocketPC
            this.Cursor = System.Windows.Forms.Cursors.Default;
#endif
            if(BoundsOfMap.HasValue && !BoundsOfMap.Value.Contains(Position))
            {
               if(Core.LastLocationInBounds.HasValue)
               {
                  Position = Core.LastLocationInBounds.Value;
               }
            }
         }
         else
         {
#if !PocketPC
            if(!selectionEnd.IsZero && !selectionStart.IsZero)
            {
               if(!SelectedArea.IsEmpty && Form.ModifierKeys == Keys.Shift)
               {
                  SetZoomToFitRect(SelectedArea);
               }
            }
#endif
         }
      }

#if !PocketPC
      protected override void OnMouseClick(MouseEventArgs e)
      {
         if(!Core.IsDragging)
         {
            System.Drawing.Point pp = FromLocalToPixel(e.X, e.Y);

            for(int i = Overlays.Count - 1; i >= 0; i--)
            {
               GMapOverlay o = Overlays[i];
               if(o != null && o.IsVisibile)
               {
                  foreach(GMapMarker m in o.Markers)
                  {
                     if(m.IsVisible && m.IsHitTestVisible)
                     {
                        if(m.LocalArea.Contains(pp))
                        {
                           if(OnMarkerClick != null)
                           {
                              OnMarkerClick(m, e);
                           }
                           break;
                        }
                     }
                  }
               }
            }
         }

         base.OnMouseClick(e);
      }
#endif

      /// <summary>
      /// apply transformation if in rotation mode
      /// </summary>
      GPoint ApplyRotationInversion(int x, int y)
      {
         GPoint ret = new GPoint(x, y);

         if(IsRotated)
         {
            System.Drawing.Point[] tt = new System.Drawing.Point[] { new System.Drawing.Point(x, y) };
            rotationMatrixInvert.TransformPoints(tt);
            var f = tt[0];

            ret.X = f.X;
            ret.Y = f.Y;
         }

         return ret;
      }

      /// <summary>
      /// apply transformation if in rotation mode
      /// </summary>
      GPoint ApplyRotation(int x, int y)
      {
         GPoint ret = new GPoint(x, y);

         if(IsRotated)
         {
            System.Drawing.Point[] tt = new System.Drawing.Point[] { new System.Drawing.Point(x, y) };
            rotationMatrix.TransformPoints(tt);
            var f = tt[0];

            ret.X = f.X;
            ret.Y = f.Y;
         }

         return ret;
      }

      protected override void OnMouseMove(MouseEventArgs e)
      {
         if(Core.IsDragging)
         {
            if(!isDragging)
            {
               isDragging = true;
               Debug.WriteLine("IsDragging = " + isDragging);
            }

            if(BoundsOfMap.HasValue && !BoundsOfMap.Value.Contains(Position))
            {
               // ...
            }
            else
            {
               Core.mouseCurrent = ApplyRotationInversion(e.X, e.Y);
               Core.Drag(Core.mouseCurrent);
               Invalidate();
            }
         }
         else
         {
#if !PocketPC
            if(isSelected && !selectionStart.IsZero && (Form.ModifierKeys == Keys.Alt || Form.ModifierKeys == Keys.Shift))
            {
               selectionEnd = FromLocalToLatLng(e.X, e.Y);
               {
                  GMap.NET.PointLatLng p1 = selectionStart;
                  GMap.NET.PointLatLng p2 = selectionEnd;

                  double x1 = Math.Min(p1.Lng, p2.Lng);
                  double y1 = Math.Max(p1.Lat, p2.Lat);
                  double x2 = Math.Max(p1.Lng, p2.Lng);
                  double y2 = Math.Min(p1.Lat, p2.Lat);

                  SelectedArea = new RectLatLng(y1, x1, x2 - x1, y1 - y2);
               }
            }
            else
#endif
            {
               System.Drawing.Point pp = FromLocalToPixel(e.X, e.Y);

               for(int i = Overlays.Count - 1; i >= 0; i--)
               {
                  GMapOverlay o = Overlays[i];
                  if(o != null && o.IsVisibile)
                  {
                     foreach(GMapMarker m in o.Markers)
                     {
                        if(m.IsVisible && m.IsHitTestVisible)
                        {
                           if(m.LocalArea.Contains(pp))
                           {
#if !PocketPC
                              this.Cursor = System.Windows.Forms.Cursors.Hand;
#endif
                              m.IsMouseOver = true;

                              Invalidate();

                              if(OnMarkerEnter != null)
                              {
                                 OnMarkerEnter(m);
                              }
                           }
                           else if(m.IsMouseOver)
                           {
#if !PocketPC
                              this.Cursor = System.Windows.Forms.Cursors.Default;
#endif
                              m.IsMouseOver = false;

                              Invalidate();

                              if(OnMarkerLeave != null)
                              {
                                 OnMarkerLeave(m);
                              }
                           }
                        }
                     }
                  }
               }
            }
         }
         base.OnMouseMove(e);
      }

#if !PocketPC

      public bool InvertedMouseWheelZooming = false;

      protected override void OnMouseEnter(EventArgs e)
      {
         base.OnMouseEnter(e);
         Focus();
      }

      protected override void OnMouseWheel(MouseEventArgs e)
      {
         base.OnMouseWheel(e);

         if(!IsMouseOverMarker && !Core.IsDragging)
         {
            if(Core.mouseLastZoom.X != e.X && Core.mouseLastZoom.Y != e.Y)
            {
               var pd = FromLocalToPixel(e.X, e.Y);
               GPoint pp = new GPoint(pd.X, pd.Y);

               if(MouseWheelZoomType == MouseWheelZoomType.MousePositionAndCenter)
               {
                  Core.centerPixel = pp;
               }
               else if(MouseWheelZoomType == MouseWheelZoomType.ViewCenter)
               {
                  pp = new GPoint(Width / 2, Height / 2);
                  pp.Offset(-Core.renderOffset.X, -Core.renderOffset.Y);
                  Core.centerPixel = pp;
               }
               else if(MouseWheelZoomType == MouseWheelZoomType.MousePositionWithoutCenter)
               {
                  Core.centerPixel = pp;
               }

               Core.mouseLastZoom.X = e.X;
               Core.mouseLastZoom.Y = e.Y;
            }

            // set mouse position to map center
            if(MouseWheelZoomType != MouseWheelZoomType.MousePositionWithoutCenter)
            {
               if(!GMaps.Instance.IsRunningOnMono)
               {
                  System.Drawing.Point p = PointToScreen(new System.Drawing.Point(Width / 2, Height / 2));
                  Stuff.SetCursorPos((int) p.X, (int) p.Y);
               }
            }

            Core.zoomPositionChanged = true;
            Core.MouseWheelZooming = true;

            if(e.Delta > 0)
            {
               if(!InvertedMouseWheelZooming)
               {
                  Zoom++;
               }
               else
               {
                  Zoom--;
               }
            }
            else if(e.Delta < 0)
            {
               if(!InvertedMouseWheelZooming)
               {
                  Zoom--;
               }
               else
               {
                  Zoom++;
               }
            }

            Core.MouseWheelZooming = false;
         }
      }
#endif
      #endregion

      #region IGControl Members

      /// <summary>
      /// Call it to empty tile cache & reload tiles
      /// </summary>
      public void ReloadMap()
      {
         Core.ReloadMap();
      }

      /// <summary>
      /// set current position using keywords
      /// </summary>
      /// <param name="keys"></param>
      /// <returns>true if successfull</returns>
      public GeoCoderStatusCode SetCurrentPositionByKeywords(string keys)
      {
         GeoCoderStatusCode status = GeoCoderStatusCode.Unknow;
         PointLatLng? pos = Manager.GetLatLngFromGeocoder(keys, out status);
         if(pos.HasValue && status == GeoCoderStatusCode.G_GEO_SUCCESS)
         {
            Position = pos.Value;
         }

         return status;
      }

      public System.Drawing.Point FromLocalToPixel(int x, int y)
      {
         System.Drawing.Point ret = new System.Drawing.Point(x, y);

         ret.Offset(-Core.renderOffset.X, -Core.renderOffset.Y);

         if(IsRotated)
         {
            System.Drawing.Point[] tt = new System.Drawing.Point[] { ret };
            rotationMatrixInvert.TransformPoints(tt);
            ret = tt[0];
         }

         return ret;
      }

      /// <summary>
      /// gets world coordinate from local control coordinate 
      /// </summary>
      /// <param name="x"></param>
      /// <param name="y"></param>
      /// <returns></returns>
      public PointLatLng FromLocalToLatLng(int x, int y)
      {
#if !PocketPC
         if(MapRenderTransform.HasValue)
         {
            // var tp = MapRenderTransform.Inverse.Transform(new System.Windows.Point(x, y));
            //x = (int) tp.X;
            //y = (int) tp.Y;
            x = (int) (x * MapRenderTransform.Value);
            y = (int) (y * MapRenderTransform.Value);
         }

         if(IsRotated)
         {
            System.Drawing.Point[] tt = new System.Drawing.Point[] { new System.Drawing.Point(x, y) };
            rotationMatrixInvert.TransformPoints(tt);
            var f = tt[0];

            x = f.X;
            y = f.Y;
         }
#endif
         return Core.FromLocalToLatLng(x, y);
      }

      /// <summary>
      /// gets local coordinate from world coordinate
      /// </summary>
      /// <param name="point"></param>
      /// <returns></returns>
      public GPoint FromLatLngToLocal(PointLatLng point)
      {
         GPoint ret = Projection.FromLatLngToPixel(point, Core.Zoom, true);

#if !PocketPC
         if(MapRenderTransform.HasValue)
         {
            //var tp = MapRenderTransform.Transform(new System.Windows.Point(ret.X, ret.Y));
            ret.X = (int) (ret.X / MapRenderTransform.Value);
            ret.Y = (int) (ret.X / MapRenderTransform.Value);
         }

         if(IsRotated)
         {
            System.Drawing.Point[] tt = new System.Drawing.Point[] { new System.Drawing.Point(ret.X, ret.Y) };
            rotationMatrix.TransformPoints(tt);
            var f = tt[0];

            ret.X = f.X;
            ret.Y = f.Y;
         }

#endif
         return ret;
      }

#if !PocketPC

      /// <summary>
      /// shows map db export dialog
      /// </summary>
      /// <returns></returns>
      public bool ShowExportDialog()
      {
         using(FileDialog dlg = new SaveFileDialog())
         {
            dlg.CheckPathExists = true;
            dlg.CheckFileExists = false;
            dlg.AddExtension = true;
            dlg.DefaultExt = "gmdb";
            dlg.ValidateNames = true;
            dlg.Title = "GMap.NET: Export map to db, if file exsist only new data will be added";
            dlg.FileName = "DataExp";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dlg.Filter = "GMap.NET DB files (*.gmdb)|*.gmdb";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;

            if(dlg.ShowDialog() == DialogResult.OK)
            {
               bool ok = GMaps.Instance.ExportToGMDB(dlg.FileName);
               if(ok)
               {
                  MessageBox.Show("Complete!", "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Information);
               }
               else
               {
                  MessageBox.Show("Failed!", "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Warning);
               }

               return ok;
            }
         }

         return false;
      }

      /// <summary>
      /// shows map dbimport dialog
      /// </summary>
      /// <returns></returns>
      public bool ShowImportDialog()
      {
         using(FileDialog dlg = new OpenFileDialog())
         {
            dlg.CheckPathExists = true;
            dlg.CheckFileExists = false;
            dlg.AddExtension = true;
            dlg.DefaultExt = "gmdb";
            dlg.ValidateNames = true;
            dlg.Title = "GMap.NET: Import to db, only new data will be added";
            dlg.FileName = "DataExp";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dlg.Filter = "GMap.NET DB files (*.gmdb)|*.gmdb";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;

            if(dlg.ShowDialog() == DialogResult.OK)
            {
               bool ok = GMaps.Instance.ImportFromGMDB(dlg.FileName);
               if(ok)
               {
                  MessageBox.Show("Complete!", "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Information);
               }
               else
               {
                  MessageBox.Show("Failed!", "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Warning);
               }

               return ok;
            }
         }

         return false;
      }
#endif

      [Category("GMap.NET"), DefaultValue(0)]
      public double Zoom
      {
         get
         {
            return zoomReal;
         }
         set
         {
            if(zoomReal != value)
            {
               Debug.WriteLine("ZoomPropertyChanged: " + zoomReal + " -> " + value);

               if(value > MaxZoom)
               {
                  zoomReal = MaxZoom;
               }
               else if(value < MinZoom)
               {
                  zoomReal = MinZoom;
               }
               else
               {
                  zoomReal = value;
               }

               float remainder = (float) System.Decimal.Remainder((Decimal) value, (Decimal) 1);
               if(remainder != 0)
               {
                  float scaleValue = remainder + 1;
                  {
#if !PocketPC
                     MapRenderTransform = scaleValue;
#endif
                  }

                  ZoomStep = Convert.ToInt32(value - remainder);
               }
               else
               {
#if !PocketPC
                  MapRenderTransform = null;
#endif
                  ZoomStep = Convert.ToInt32(value);
                  zoomReal = ZoomStep;
               }

               if(Core.IsStarted && !IsDragging)
               {
                  ForceUpdateOverlays();
               }
            }
         }
      }

      /// <summary>
      /// map zoom level
      /// </summary>
      [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      internal int ZoomStep
      {
         get
         {
            return Core.Zoom;
         }
         set
         {
            if(value > MaxZoom)
            {
               Core.Zoom = MaxZoom;
            }
            else if(value < MinZoom)
            {
               Core.Zoom = MinZoom;
            }
            else
            {
               Core.Zoom = value;
            }
         }
      }

      /// <summary>
      /// current map center position
      /// </summary>
      [Browsable(false)]
      public PointLatLng Position
      {
         get
         {
            return Core.CurrentPosition;
         }
         set
         {
            Core.CurrentPosition = value;

            if(Core.IsStarted)
            {
               ForceUpdateOverlays();
            }
         }
      }

      /// <summary>
      /// location of cache
      /// </summary>
      [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      public string CacheLocation
      {
         get
         {
            return Cache.Instance.CacheLocation;
         }
         set
         {
            Cache.Instance.CacheLocation = value;
         }
      }

      bool isDragging = false;

      /// <summary>
      /// is user dragging map
      /// </summary>
      [Browsable(false)]
      public bool IsDragging
      {
         get
         {
            return isDragging;
         }
      }

      bool isMouseOverMarker;

      /// <summary>
      /// is mouse over marker
      /// </summary>
      [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      public bool IsMouseOverMarker
      {
         get
         {
            return isMouseOverMarker;
         }
         internal set
         {
            isMouseOverMarker = value;
         }
      }

      /// <summary>
      /// gets current map view top/left coordinate, width in Lng, height in Lat
      /// </summary>
      [Browsable(false)]
      public RectLatLng ViewArea
      {
         get
         {
            return Core.CurrentViewArea;
         }
      }

      [Browsable(false)]
      public GMap.NET.GRect ViewAreaPixel
      {
         get
         {
            return Core.viewRectPixel;
         }
      }

      [Browsable(false)]
      public GMap.NET.GPoint PositionPixel
      {
         get
         {
            return Core.centerPixel;
         }
      }

      /// <summary>
      /// type of map
      /// </summary>
      [Category("GMap.NET"), DefaultValue(MapType.None)]
      public MapType MapType
      {
         get
         {
            return Core.MapType;
         }
         set
         {
            if(Core.MapType != value)
            {
               Debug.WriteLine("MapType: " + Core.MapType + " -> " + value);

               RectLatLng viewarea = SelectedArea;
               if(Core.IsStarted)
               {
                  if(viewarea != RectLatLng.Empty)
                  {
                     Position = new PointLatLng(viewarea.Lat - viewarea.HeightLat / 2, viewarea.Lng + viewarea.WidthLng / 2);
                  }
                  else
                  {
                     viewarea = ViewArea;
                  }
               }

               Core.MapType = value;

               if(Core.IsStarted)
               {
                  int bestZoom = ZoomStep;

                  if(Core.zoomArea.HasValue)
                  {
                     bestZoom = Core.GetMaxZoomToFitRect(Core.zoomArea.Value);
                  }
                  else if(viewarea != RectLatLng.Empty)
                  {
                     bestZoom = Core.GetMaxZoomToFitRect(viewarea);
                  }

                  if(bestZoom > MaxZoom)
                  {
                     bestZoom = MaxZoom;
                  }

                  Core.UpdateFieldsOnZoomChanged(bestZoom);

                  zoomReal = bestZoom;

                  ForceUpdateOverlays();

                  Core.OnMapSizeChanged(Width, Height, false);
                  Core.GoToCurrentPositionOnZoom();
                  Core.ReloadMap();
                  Core.RaiseMapTypeChangedEvent();
               }
            }
         }
      }

      /// <summary>
      /// map projection
      /// </summary>
      [Browsable(false)]
      public PureProjection Projection
      {
         get
         {
            return Core.Projection;
         }
      }

      /// <summary>
      /// is routes enabled
      /// </summary>
      [Category("GMap.NET")]
      public bool RoutesEnabled
      {
         get
         {
            return Core.RoutesEnabled;
         }
         set
         {
            Core.RoutesEnabled = value;
         }
      }

      /// <summary>
      /// is polygons enabled
      /// </summary>
      [Category("GMap.NET")]
      public bool PolygonsEnabled
      {
         get
         {
            return Core.PolygonsEnabled;
         }
         set
         {
            Core.PolygonsEnabled = value;
         }
      }

      /// <summary>
      /// is markers enabled
      /// </summary>
      [Category("GMap.NET")]
      public bool MarkersEnabled
      {
         get
         {
            return Core.MarkersEnabled;
         }
         set
         {
            Core.MarkersEnabled = value;
         }
      }

      /// <summary>
      /// can user drag map
      /// </summary>
      [Category("GMap.NET")]
      public bool CanDragMap
      {
         get
         {
            return Core.CanDragMap;
         }
         set
         {
            Core.CanDragMap = value;
         }
      }

      /// <summary>
      /// map render mode
      /// </summary>
      [Browsable(false)]
      public RenderMode RenderMode
      {
         get
         {
            return Core.RenderMode;
         }
         internal set
         {
            Core.RenderMode = value;
         }
      }

      /// <summary>
      /// gets map manager
      /// </summary>
      [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      public GMaps Manager
      {
         get
         {
            return GMaps.Instance;
         }
      }

      #endregion

      #region IGControl event Members

      /// <summary>
      /// occurs when current position is changed
      /// </summary>
      public event CurrentPositionChanged OnCurrentPositionChanged
      {
         add
         {
            Core.OnCurrentPositionChanged += value;
         }
         remove
         {
            Core.OnCurrentPositionChanged -= value;
         }
      }

      /// <summary>
      /// occurs when tile set load is complete
      /// </summary>
      public event TileLoadComplete OnTileLoadComplete
      {
         add
         {
            Core.OnTileLoadComplete += value;
         }
         remove
         {
            Core.OnTileLoadComplete -= value;
         }
      }

      /// <summary>
      /// occurs when tile set is starting to load
      /// </summary>
      public event TileLoadStart OnTileLoadStart
      {
         add
         {
            Core.OnTileLoadStart += value;
         }
         remove
         {
            Core.OnTileLoadStart -= value;
         }
      }

      /// <summary>
      /// occurs on map drag
      /// </summary>
      public event MapDrag OnMapDrag
      {
         add
         {
            Core.OnMapDrag += value;
         }
         remove
         {
            Core.OnMapDrag -= value;
         }
      }

      /// <summary>
      /// occurs on map zoom changed
      /// </summary>
      public event MapZoomChanged OnMapZoomChanged
      {
         add
         {
            Core.OnMapZoomChanged += value;
         }
         remove
         {
            Core.OnMapZoomChanged -= value;
         }
      }

      /// <summary>
      /// occures on map type changed
      /// </summary>
      public event MapTypeChanged OnMapTypeChanged
      {
         add
         {
            Core.OnMapTypeChanged += value;
         }
         remove
         {
            Core.OnMapTypeChanged -= value;
         }
      }

      /// <summary>
      /// occurs on empty tile displayed
      /// </summary>
      public event EmptyTileError OnEmptyTileError
      {
         add
         {
            Core.OnEmptyTileError += value;
         }
         remove
         {
            Core.OnEmptyTileError -= value;
         }
      }

      #endregion

      #region Serialization

      static readonly BinaryFormatter BinaryFormatter = new BinaryFormatter();

      /// <summary>
      /// Serializes the overlays.
      /// </summary>
      /// <param name="stream">The stream.</param>
      public void SerializeOverlays(Stream stream)
      {
         if(stream == null)
         {
            throw new ArgumentNullException("stream");
         }

         // Create an array from the overlays
         GMapOverlay[] overlayArray = new GMapOverlay[this.Overlays.Count];
         this.Overlays.CopyTo(overlayArray, 0);

         // Serialize the overlays
         BinaryFormatter.Serialize(stream, overlayArray);
      }

      /// <summary>
      /// De-serializes the overlays.
      /// </summary>
      /// <param name="stream">The stream.</param>
      public void DeserializeOverlays(Stream stream)
      {
         if(stream == null)
         {
            throw new ArgumentNullException("stream");
         }

         // De-serialize the overlays
         GMapOverlay[] overlayArray = BinaryFormatter.Deserialize(stream) as GMapOverlay[];

         // Populate the collection of overlays.
         foreach(GMapOverlay overlay in overlayArray)
         {
            overlay.Control = this;
            this.Overlays.Add(overlay);
         }

         this.ForceUpdateOverlays();
      }

      #endregion
   }
}
