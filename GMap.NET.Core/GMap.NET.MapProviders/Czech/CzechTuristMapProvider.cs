﻿
namespace GMap.NET.MapProviders
{
   using System;

   /// <summary>
   /// CzechTuristMap provider, http://www.mapy.cz/
   /// </summary>
   public class CzechTuristMapProvider : CzechMapProviderBase
   {
      public static readonly CzechTuristMapProvider Instance;

      CzechTuristMapProvider()
      {
      }

      static CzechTuristMapProvider()
      {
         Instance = new CzechTuristMapProvider();
      }

      #region GMapProvider Members

      readonly Guid id = new Guid("B923C81D-880C-42EB-88AB-AF8FE42B564D");
      public override Guid Id
      {
         get
         {
            return id;
         }
      }

      readonly string name = "CzechTuristMap";
      public override string Name
      {
         get
         {
            return name;
         }
      }

      public override PureImage GetTileImage(GPoint pos, int zoom)
      {
         string url = MakeTileImageUrl(pos, zoom, LanguageStr);

         return GetTileImageUsingHttp(url);
      }

      #endregion

      string MakeTileImageUrl(GPoint pos, int zoom, string language)
      {
         // http://m3.mapserver.mapy.cz/wtourist-m/14-8802-5528

         return string.Format(UrlFormat, GetServerNum(pos, 3) + 1, zoom, pos.X, pos.Y);
      }

      static readonly string UrlFormat = "http://m{0}.mapserver.mapy.cz/wturist-m/{1}-{2}-{3}";
   }
}