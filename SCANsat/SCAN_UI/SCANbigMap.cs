﻿#region license
/* 
 *  [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 *
 * SCANsat - New Big map window object
 * 
 * Copyright (c)2013 damny;
 * Copyright (c)2014 David Grandy <david.grandy@gmail.com>;
 * Copyright (c)2014 technogeeky <technogeeky@gmail.com>;
 * Copyright (c)2014 (Your Name Here) <your email here>; see LICENSE.txt for licensing details.
 *
 */
#endregion

using System.Collections.Generic;
using SCANsat.SCAN_Platform;
using SCANsat;
using SCANsat.SCAN_UI.UI_Framework;
using SCANsat.SCAN_Data;
using SCANsat.SCAN_Map;
using palette = SCANsat.SCAN_UI.UI_Framework.SCANpalette;
using UnityEngine;

namespace SCANsat.SCAN_UI
{
	class SCANBigMap : SCAN_MBW
	{
		private static SCANmap bigmap;
		private static CelestialBody b;
		private string mapTypeTitle = "";
		private SCANdata data;
		private Vessel v;
		private float resizeW, dragX;
		private bool drawGrid, currentGrid, currentColor, lastColor, lastResource;
		private bool drop_down_open, projection_drop_down, mapType_drop_down, resources_drop_down, planetoid_drop_down;
		//private Texture2D overlay_static;
		private Dictionary<int, List<List<Vector2d>>> gridLines = new Dictionary<int, List<List<Vector2d>>>();
		private Rect ddRect, zoomCloseRect;
		private Rect rc = new Rect(0, 0, 20, 20);
		private Vector2 scrollP, scrollR;
		private Rect pos_spotmap = new Rect(10f, 10f, 10f, 10f);
		private Rect pos_spotmap_x = new Rect(10f, 10f, 25f, 25f);
		internal static Rect defaultRect = new Rect(250, 60, 780, 460);

		private string exportSize = "";

		//private bool overlay = false;
		//private int step = 0;

		internal SCANzoomWindow spotMap;

		private List<SCANresourceGlobal> loadedResources = new List<SCANresourceGlobal>();

		//Values used for the orbit overlay - Need to fix this
		internal static int[] eq_an_map, eq_dn_map;
		internal static Texture2D eq_map;
		internal static int eq_frame;

		protected override void Awake()
		{
			WindowCaption = "Map of ";
			WindowRect = defaultRect;
			WindowSize_Min = new Vector2(550, 225);
			WindowOptions = new GUILayoutOption[2] { GUILayout.Width(600), GUILayout.Height(300) };
			WindowStyle = SCANskins.SCAN_window;
			Visible = false;
			DragEnabled = true;
			ClampEnabled = false;
			TooltipMouseOffset = new Vector2d(-10, -25);

			SCAN_SkinsLibrary.SetCurrent("SCAN_Unity");
			SCAN_SkinsLibrary.SetCurrentTooltip();
		}

		protected override void Start()
		{
			//Initialize the map object
			Visible = SCANcontroller.controller.bigMapVisible;
			WindowRect.x = SCANcontroller.controller.map_x;
			WindowRect.y = SCANcontroller.controller.map_y;
			lastColor = currentColor = SCANcontroller.controller.colours == 0;
			lastResource = SCANcontroller.controller.map_ResourceOverlay;
			if (SCANconfigLoader.GlobalResource)
				loadedResources = SCANcontroller.setLoadedResourceList();
			TooltipsEnabled = SCANcontroller.controller.toolTips;

			initializeMap();
		}

		private void initializeMap()
		{
			v = FlightGlobals.ActiveVessel;
			if (b == null)
			{
				if (v == null)
					b = FlightGlobals.Bodies[1];
				else
					b = v.mainBody;
			}

			if (bigmap == null)
			{
				bigmap = new SCANmap(b, true);
				bigmap.setProjection((MapProjection)SCANcontroller.controller.projection);
				if (SCANcontroller.controller.map_width % 2 != 0)
					SCANcontroller.controller.map_width += 1;
				bigmap.setWidth(SCANcontroller.controller.map_width);
			}

			data = SCANUtil.getData(b);
			if (data == null)
			{
				data = new SCANdata(b);
				SCANcontroller.controller.addToBodyData(b, data);
			}
			bigmap.setBody(b);

			WindowCaption = string.Format("Map of {0}", b.theName);
		}

		protected override void OnDestroy()
		{
			if (spotMap != null)
				Destroy(spotMap);
		}

		protected override void Update()
		{
			if (FlightGlobals.ready)
				v = FlightGlobals.ActiveVessel;
		}

		//Properties used to sync with color selection window
		public static SCANmap BigMap
		{
			get { return bigmap; }
		}

		public SCANdata Data
		{
			get { return data; }
		}

		public Vessel V
		{
			get { return v; }
		}

		public CelestialBody Body
		{
			get { return b; }
		}

		protected override void DrawWindowPre(int id)
		{
			//Append the map type to the window caption
			if (bigmap != null)
				mapTypeTitle = SCANmapType.mapTypeNames[(int)bigmap.MType];
			else
				mapTypeTitle = "";

			WindowCaption = string.Format("{0} Map of {1}", mapTypeTitle, bigmap.Body.theName);

			//Re-sizing code; moved here from SCAN_MBW
			if (IsResizing && !inRepaint())
			{
				if (Input.GetMouseButtonUp(0))
				{
					IsResizing = false;
					if (resizeW < WindowSize_Min.x)
						resizeW = WindowSize_Min.x;
					if ((int)resizeW % 2 != 0)
						resizeW += 1;
					bigmap.setWidth((int)resizeW);
					drawGrid = true;
					SCANcontroller.controller.map_width = bigmap.MapWidth;
				}
				else
				{
					float xx = Input.mousePosition.x;
					resizeW += xx - dragX;
					dragX = xx;
				}
				if (Event.current.isMouse)
					Event.current.Use();
			}

			//Disable any errant drop down menus
			if (!drop_down_open)
			{
				projection_drop_down = false;
				mapType_drop_down = false;
				resources_drop_down = false;
				planetoid_drop_down = false;
			}
		}

		//The primary GUI method
		protected override void DrawWindow(int id)
		{
			versionLabel(id);		/* Standard version label and close button */
			closeBox(id);

			growS();
				topMenu(id);		/* Top row of buttons - used to control the map types */
				growE();
					toggleBar(id);	/* Toggle options along left side - control overlay options - *Replace buttons with		textures*    */
					fillS(60);
					mapDraw(id);	/* Draw the main map texture */
				stopE();
				growE();
					fillS(180);
					growS();
						mouseOver(id);		/* Handle all mouse-over info and zoom-map code */
						legendBar(id);		/* Draw the mouseover info and legend bar along the bottom */
					stopS();
				stopE();
			stopS();

			mapLabels(id);			/* Draw the vessel/anomaly icons on the map */
			if (drop_down_open)
				dropDown(id);		/* Draw the drop down menus if any are open */
		}

		protected override void DrawWindowPost(int id)
		{
			//Close the drop down menu if the window is clicked anywhere else
			if (drop_down_open && Event.current.type == EventType.mouseDown && !ddRect.Contains(Event.current.mousePosition))
				drop_down_open = false;

			//Update black and white/color statuse
			if (lastColor != currentColor)
			{
				lastColor = currentColor;
				if (SCANcontroller.controller.colours == 0)
					SCANcontroller.controller.colours = 1;
				else
					SCANcontroller.controller.colours = 0;
				bigmap.resetMap(SCANcontroller.controller.map_ResourceOverlay);
				SCANcontroller.controller.mainMap.resetImages();
			}

			//Update grid overlay status
			if (currentGrid != SCANcontroller.controller.map_grid)
			{
				currentGrid = SCANcontroller.controller.map_grid;
				drawGrid = true;
			}

			//Update selected resource status
			if (lastResource != SCANcontroller.controller.map_ResourceOverlay)
			{
				lastResource = SCANcontroller.controller.map_ResourceOverlay;
				bigmap.resetMap(SCANcontroller.controller.map_ResourceOverlay);
			}

			SCANcontroller.controller.map_x = (int)WindowRect.x;
			SCANcontroller.controller.map_y = (int)WindowRect.y;
		}

		//Draw version label in upper left corner
		private void versionLabel(int id)
		{
			Rect r = new Rect(6, 0, 50, 18);
			GUI.Label(r, SCANmainMenuLoader.SCANsatVersion, SCANskins.SCAN_whiteReadoutLabel);
		}

		//Draw the close button in upper right corner
		private void closeBox(int id)
		{
			Rect r = new Rect(WindowRect.width - 20, 1, 18, 18);
			if (GUI.Button(r, SCANcontroller.controller.closeBox, SCANskins.SCAN_closeButton))
			{
				Visible = false;
				SCANcontroller.controller.bigMapVisible = Visible;
			}
		}

		//Draw the drop down buttons along the top of the map - Used to control map types
		private void topMenu(int id)
		{
			growE();
				fillS(100);
				if (GUILayout.Button("Projection", GUILayout.MaxWidth(100)))
				{
					projection_drop_down = !projection_drop_down;
					drop_down_open = !drop_down_open;
				}
				fillS(40);
				if (GUILayout.Button("Map Type", GUILayout.MaxWidth(90)))
				{
					mapType_drop_down = !mapType_drop_down;
					drop_down_open = !drop_down_open;
				}
				fillS();
				if (GUILayout.Button(iconWithTT(SCANskins.SCAN_RefreshIcon, "Refresh Map"), SCANskins.SCAN_buttonBorderless, GUILayout.MaxWidth(34), GUILayout.MaxHeight(28)))
				{
					bigmap.resetMap(SCANcontroller.controller.map_ResourceOverlay);
				}
				fillS();
				if (SCANconfigLoader.GlobalResource)
				{
					if (GUILayout.Button("Resources", GUILayout.MaxWidth(90)))
					{
						resources_drop_down = !resources_drop_down;
						drop_down_open = !drop_down_open;
					}
					fillS(40);
				}
				if (GUILayout.Button("Celestial Body", GUILayout.MaxWidth(110)))
				{
					planetoid_drop_down = !planetoid_drop_down;
					drop_down_open = !drop_down_open;
				}
				fillS(20);
			stopE();
		}

		//Draw the overlay options along the left side of the map texture
		private void toggleBar(int id)
		{
			growS();

				currentColor = GUILayout.Toggle(currentColor, textWithTT("", "Toggle Color"));

				Rect d = GUILayoutUtility.GetLastRect();
				d.x += 34;
				d.y += 2;
				d.width = 48;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_ColorToggleIcon, "Toggle Color"), SCANskins.SCAN_buttonBorderless))
				{
					currentColor = !currentColor;
				}

				fillS();

				SCANcontroller.controller.map_grid = GUILayout.Toggle(SCANcontroller.controller.map_grid, textWithTT("", "Toggle Grid"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 34;
				d.y += 2;
				d.width = 48;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_GridIcon, "Toggle Grid"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.map_grid = !SCANcontroller.controller.map_grid;
				}

				fillS();

				SCANcontroller.controller.map_orbit = GUILayout.Toggle(SCANcontroller.controller.map_orbit, textWithTT("", "Toggle Orbit"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 34;
				d.y += 2;
				d.width = 48;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_OrbitIcon, "Toggle Orbit"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.map_orbit = !SCANcontroller.controller.map_orbit;
				}

				fillS();

				SCANcontroller.controller.map_waypoints = GUILayout.Toggle(SCANcontroller.controller.map_waypoints, textWithTT("", "Toggle Waypoints"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 44;
				d.y += 2;
				d.width = 24;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_WaypointIcon, "Toggle Waypoints"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.map_waypoints = !SCANcontroller.controller.map_waypoints;
				}

				fillS();

				SCANcontroller.controller.map_markers = GUILayout.Toggle(SCANcontroller.controller.map_markers, textWithTT("", "Toggle Anomalies"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 44;
				d.y += 2;
				d.width = 24;
				d.height = 24;

				if (GUI.Button(d, textWithTT(SCANcontroller.controller.anomalyMarker, "Toggle Anomalies"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.map_markers = !SCANcontroller.controller.map_markers;
				}

				fillS();

				SCANcontroller.controller.map_flags = GUILayout.Toggle(SCANcontroller.controller.map_flags, textWithTT("", "Toggle Flags"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 44;
				d.y += 2;
				d.width = 24;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_FlagIcon, "Toggle Flags"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.map_flags = !SCANcontroller.controller.map_flags;
				}

				fillS();

				SCANcontroller.controller.map_asteroids = GUILayout.Toggle(SCANcontroller.controller.map_asteroids, textWithTT("", "Toggle Asteroids"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 44;
				d.y += 2;
				d.width = 24;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_AsteroidIcon, "Toggle Asteroids"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.map_asteroids = !SCANcontroller.controller.map_asteroids;
				}

				fillS();

				SCANcontroller.controller.legend = GUILayout.Toggle(SCANcontroller.controller.legend, textWithTT("", "Toggle Legend"));

				d = GUILayoutUtility.GetLastRect();
				d.x += 34;
				d.y += 2;
				d.width = 48;
				d.height = 24;

				if (GUI.Button(d, iconWithTT(SCANskins.SCAN_LegendIcon, "Toggle Legend"), SCANskins.SCAN_buttonBorderless))
				{
					SCANcontroller.controller.legend = !SCANcontroller.controller.legend;
				}

				if (SCANconfigLoader.GlobalResource)
				{
					fillS();

					SCANcontroller.controller.map_ResourceOverlay = GUILayout.Toggle(SCANcontroller.controller.map_ResourceOverlay, textWithTT("", "Toggle Resources"));

					d = GUILayoutUtility.GetLastRect();
					d.x += 44;
					d.y += 2;
					d.width = 24;
					d.height = 24;

					if (GUI.Button(d, iconWithTT(SCANskins.SCAN_ResourceIcon, "Toggle Resources"), SCANskins.SCAN_buttonBorderless))
					{
						SCANcontroller.controller.map_ResourceOverlay = !SCANcontroller.controller.map_ResourceOverlay;
					}
				}
			stopS();

			//Open all four windows using icons instead of text; use tooltips
			Rect s = new Rect(10, WindowRect.height - 42, 32, 32);

			if (GUI.Button(s, iconWithTT(SCANskins.SCAN_SmallMapIcon, "Small Map"), SCANskins.SCAN_windowButton))
			{
				SCANcontroller.controller.mainMap.Visible = !SCANcontroller.controller.mainMap.Visible;
			}

			s.x += 36;

			if (GUI.Button(s, iconWithTT(SCANskins.SCAN_InstrumentIcon, "Instrument Window"), SCANskins.SCAN_windowButton))
			{
				SCANcontroller.controller.instrumentsWindow.Visible = !SCANcontroller.controller.instrumentsWindow.Visible;
			}

			s.x += 36;

			if (GUI.Button(s, iconWithTT(SCANskins.SCAN_SettingsIcon, "Settings Menu"), SCANskins.SCAN_windowButton))
			{
				SCANcontroller.controller.settingsWindow.Visible = !SCANcontroller.controller.settingsWindow.Visible;
			}

			s.x += 36;

			if (GUI.Button(s, iconWithTT(SCANskins.SCAN_ColorIcon, "Color Control"), SCANskins.SCAN_windowButton))
			{
				SCANcontroller.controller.colorManager.Visible = !SCANcontroller.controller.colorManager.Visible;
			}

			s.x += 36;

			if (GUI.Button(s, iconWithTT(SCANskins.SCAN_OverlayIcon, "Overlay Control"), SCANskins.SCAN_windowButton))
			{
				SCANcontroller.controller.resourceOverlay.Visible = !SCANcontroller.controller.resourceOverlay.Visible;
			}

			s.x = WindowRect.width - 66;

			if (GUI.Button(s, iconWithTT(SCANskins.SCAN_ScreenshotIcon, "Export Map"), SCANskins.SCAN_windowButton))
			{
				if (bigmap.isMapComplete())
					bigmap.exportPNG();
			}

#if DEBUG
			s.x -= 74;
			s.y -= 30;
			s.width = 60;
			s.height = 24;

			exportSize = GUI.TextField(s, exportSize, 4);

			s.x += 65;

			if (GUI.Button(s, "Set"))
			{
				int i = 0;
				if (int.TryParse(exportSize, out i))
				{
					bigmap.setWidth(i);
					drawGrid = true;
					SCANcontroller.controller.map_width = bigmap.MapWidth;
				}
			}
#endif
		}

		private void mapDraw(int id)
		{
			MapTexture = bigmap.getPartialMap();

			//A blank label used as a template for the actual map texture
			if (IsResizing)
			{
				//Set minimum map size during re-sizing
				dW = resizeW;
				if (dW < WindowSize_Min.x)
					dW = WindowSize_Min.x;
				dH = dW / 2f;
				GUILayout.Label("", GUILayout.Width(dW), GUILayout.Height(dH));
			}
			else
			{
				GUILayout.Label("", GUILayout.Width(MapTexture.width), GUILayout.Height(MapTexture.height));
			}

			TextureRect = GUILayoutUtility.GetLastRect();
			TextureRect.width = bigmap.MapWidth;
			TextureRect.height = bigmap.MapHeight;

			//Generate the grid lines
			if (drawGrid)
			{
				gridLines = new Dictionary<int, List<List<Vector2d>>>();
				gridLines = SCANuiUtil.drawGridLine(TextureRect, bigmap);
				drawGrid = false;
			}

			//Stretches the existing map while re-sizing
			if (IsResizing)
			{
				TextureRect.width = dW;
				TextureRect.height = dH;
				GUI.DrawTexture(TextureRect, MapTexture, ScaleMode.StretchToFill);
			}
			else
			{
				GUI.DrawTexture(TextureRect, MapTexture);
			}

			//Add the North/South labels to the polar projection
			if (bigmap.Projection == MapProjection.Polar)
			{
				rc.x = TextureRect.x + TextureRect.width / 2 - TextureRect.width / 8;
				rc.y = TextureRect.y + TextureRect.height / 8;
				SCANuiUtil.drawLabel(rc, "S", SCANskins.SCAN_orbitalLabelOff, true, SCANskins.SCAN_shadowReadoutLabel, false, SCANskins.SCAN_orbitalLabelOn, true);
				rc.x = TextureRect.x + TextureRect.width / 2 + TextureRect.width / 8;
				SCANuiUtil.drawLabel(rc, "N", SCANskins.SCAN_orbitalLabelOff, true, SCANskins.SCAN_shadowReadoutLabel, false, SCANskins.SCAN_orbitalLabelOn, true);
			}

			if (SCANcontroller.controller.map_grid && !IsResizing)
			{
				if (gridLines.Count > 0 && inRepaint())
				{
					GL.PushMatrix();
					foreach (List<Vector2d> points in gridLines[0])
					{
						SCANuiUtil.drawGridLines(points, bigmap.MapWidth, TextureRect.x, TextureRect.y, SCANuiUtil.blackLineColor);
					}
					foreach (List<Vector2d> points in gridLines[1])
					{
						SCANuiUtil.drawGridLines(points, bigmap.MapWidth, TextureRect.x, TextureRect.y, SCANuiUtil.lineColor);
					}
					GL.PopMatrix();
				}
			}

			//Draw the orbit overlays
			if (SCANcontroller.controller.map_orbit)
			{
				SCANuiUtil.drawOrbit(TextureRect, bigmap, v, bigmap.Body);
			}
		}

		//Display info for mouse over in the map and handle the zoom map
		private void mouseOver(int id)
		{
			float mx = Event.current.mousePosition.x - TextureRect.x;
			float my = Event.current.mousePosition.y - TextureRect.y;
			bool in_map = false;
			double mlon = 0, mlat = 0;

			//Draw the re-size label in the corner
			Rect resizer = new Rect(WindowRect.width - 24, WindowRect.height - 26, 24, 24);
			GUI.Label(resizer, SCANskins.SCAN_ResizeIcon);

			//Handles mouse positioning and converting to lat/long coordinates
			if (mx >= 0 && my >= 0 && mx < MapTexture.width && my < MapTexture.height)
			{
				double mlo = (mx * 360f / MapTexture.width) - 180;
				double mla = 90 - (my * 180f / MapTexture.height);
				mlon = bigmap.unprojectLongitude(mlo, mla);
				mlat = bigmap.unprojectLatitude(mlo, mla);

				if (mlon >= -180 && mlon <= 180 && mlat >= -90 && mlat <= 90)
				{
					in_map = true;
				}
			}

			//Handles mouse click while inside map; opens zoom map
			if (Event.current.isMouse && !ddRect.Contains(Event.current.mousePosition) && !zoomCloseRect.Contains(Event.current.mousePosition))
			{
				if (Event.current.type == EventType.MouseUp)
				{
					if (Event.current.button == 1)
					{
						if (in_map)
						{
							if (spotMap == null)
							{
								spotMap = gameObject.AddComponent<SCANzoomWindow>();
							}
							spotMap.setMapCenter(mlat, mlon, true, bigmap);
						}
						Event.current.Use();
					}
				}
				//Handle clicking inside the re-size button
				else if (Event.current.isMouse
				&& Event.current.type == EventType.MouseDown
				&& Event.current.button == 0
				&& resizer.Contains(Event.current.mousePosition))
				{
					IsResizing = true;
					dragX = Input.mousePosition.x;
					resizeW = TextureRect.width;
					Event.current.Use();
				}
			}

			//Draw the actual mouse over info label below the map
			SCANuiUtil.mouseOverInfo(mlon, mlat, bigmap, data, bigmap.Body, in_map);
		}

		//Draw the altitude legend bar along the bottom
		private void legendBar(int id)
		{
			if (bigmap.MType == mapType.Altimetry && SCANcontroller.controller.legend)
			{
				if (bigmap.MapLegend == null)
					bigmap.MapLegend = new SCANmapLegend();
				bigmap.MapLegend.Legend = bigmap.MapLegend.getLegend(data.TerrainConfig.MinTerrain, data.TerrainConfig.MaxTerrain, SCANcontroller.controller.colours, data.TerrainConfig);
				SCANuiUtil.drawLegend(data, bigmap.MapLegend);
			}
		}

		//Draw the map overlay labels
		private void mapLabels(int id)
		{
			SCANuiUtil.drawMapLabels(TextureRect, v, bigmap, data, bigmap.Body, SCANcontroller.controller.map_markers, SCANcontroller.controller.map_waypoints);
		}

		//Draw the drop down menus if any have been opened
		private void dropDown(int id)
		{
			if (projection_drop_down)
			{
				ddRect = new Rect(110, 45, 100, 70);
				GUI.Box(ddRect, "");
				for (int i = 0; i < SCANmapProjection.projectionNames.Length; ++i)
				{
					Rect r = new Rect(ddRect.x + 2, ddRect.y + (24 * i), ddRect.width - 4, 20);
					if (GUI.Button(r, SCANmapProjection.projectionNames[i], SCANcontroller.controller.projection == i ? SCANskins.SCAN_dropDownButtonActive : SCANskins.SCAN_dropDownButton))
					{
						bigmap.setProjection((MapProjection)i);
						bigmap.resetMap(SCANcontroller.controller.map_ResourceOverlay);
						SCANcontroller.controller.projection = i;
						drawGrid = true;
						drop_down_open = false;
					}
				}
			}

			else if (mapType_drop_down)
			{
				ddRect = new Rect(270, 45, 70, 70);
				GUI.Box(ddRect, "");
				for (int i = 0; i < SCANmapType.mapTypeNames.Length; i++)
				{
					Rect r = new Rect(ddRect.x + 2, ddRect.y + (24 * i), ddRect.width - 4, 20);
					if (GUI.Button(r, SCANmapType.mapTypeNames[i], (int)(bigmap.MType) == i ? SCANskins.SCAN_dropDownButtonActive : SCANskins.SCAN_dropDownButton))
					{
						bigmap.resetMap((mapType)i, true, SCANcontroller.controller.map_ResourceOverlay);
						drop_down_open = false;
					}
				}
			}

			else if (resources_drop_down)
			{
				ddRect = new Rect(WindowRect.width - 290, 45, 120, 160);
				GUI.Box(ddRect, "");
				for (int i = 0; i < loadedResources.Count; i++)
				{
					scrollR = GUI.BeginScrollView(ddRect, scrollR, new Rect(0, 0, 100, 20 * loadedResources.Count));
					Rect r = new Rect(2, 20 * i, 96, 20);
					if (GUI.Button(r, loadedResources[i].Name, SCANcontroller.controller.resourceSelection == loadedResources[i].Name ? SCANskins.SCAN_dropDownButtonActive : SCANskins.SCAN_dropDownButton))
					{
						bigmap.Resource = loadedResources[i];
						bigmap.Resource.CurrentBodyConfig(bigmap.Body.name);

						SCANcontroller.controller.resourceSelection = bigmap.Resource.Name;

						if (SCANcontroller.controller.map_ResourceOverlay)
						{
							bigmap.resetMap(SCANcontroller.controller.map_ResourceOverlay);
						}

						drop_down_open = false;
					}
					GUI.EndScrollView();
				}
			}

			else if (planetoid_drop_down)
			{
				int j = 0;
				ddRect = new Rect(WindowRect.width - 130, 45, 100, 160);
				GUI.Box(ddRect, "");
				for (int i = 0; i < SCANcontroller.controller.GetDataCount; i++)
				{
					scrollP = GUI.BeginScrollView(ddRect, scrollP, new Rect(0, 0, 80, (20 * SCANcontroller.controller.GetDataCount) + 1));

					SCANdata dropDownData = SCANcontroller.controller.getData(i);
					if (dropDownData != null)
					{
						Rect r = new Rect(2, 20 * j, 76, 20);
						if (GUI.Button(r, dropDownData.Body.name, b.name == dropDownData.Body.name ? SCANskins.SCAN_dropDownButtonActive : SCANskins.SCAN_dropDownButton))
						{
							data = dropDownData;
							b = data.Body;
							bigmap.setBody(data.Body);
							bigmap.resetMap(SCANcontroller.controller.map_ResourceOverlay);
							drop_down_open = false;
						}
						j++;
					}
					GUI.EndScrollView();
				}
			}
			else
				drop_down_open = false;
		}

	}
}
