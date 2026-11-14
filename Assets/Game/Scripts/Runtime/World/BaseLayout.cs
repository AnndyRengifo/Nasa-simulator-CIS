using System.Collections.Generic;
using Sol3.Game.Art;
using UnityEngine;

namespace Sol3.Game.World
{
    public enum RoomId
    {
        Control,
        LifeSupport,
        Greenhouse,
        Lab,
        Engine,
        Shelter,
        Corridor,
    }

    public sealed class RoomDef
    {
        public RoomId Id;
        public string Name;
        public RectInt Rect;
        public Color Floor;
        public int FloorStyle;
        public Color Light;

        public Vector2 Center => new Vector2(Rect.x + Rect.width * 0.5f, Rect.y + Rect.height * 0.5f);
        public Vector2 At(float x, float y) => new Vector2(Rect.x + x, Rect.y + y);
    }

    public enum MachineKind
    {
        ControlTerminal,
        CommsConsole,
        OxygenGenerator,
        WaterRecycler,
        CropBed,
        GreenhouseLights,
        ResearchBench,
        BatteryBank,
        PowerPanel,
        ShelterConsole,
        RadiationMonitor,
    }

    /// <summary>Plano de Base Aurora: seis habitaciones conectadas por pasillos (1 unidad = 1 baldosa).</summary>
    public static class BaseLayout
    {
        public const int Width = 41;
        public const int Height = 19;

        public static readonly RoomDef[] Rooms =
        {
            new RoomDef { Id = RoomId.Lab, Name = "LABORATORIO", Rect = new RectInt(1, 10, 10, 7), Floor = Palette.Hex("#AEB9C2"), FloorStyle = 3, Light = Palette.Hex("#DDEBFF") },
            new RoomDef { Id = RoomId.Control, Name = "CENTRO DE CONTROL", Rect = new RectInt(15, 10, 11, 7), Floor = Palette.Hex("#34495E"), FloorStyle = 0, Light = Palette.Hex("#9EDCEA") },
            new RoomDef { Id = RoomId.LifeSupport, Name = "SOPORTE VITAL", Rect = new RectInt(30, 10, 10, 7), Floor = Palette.Hex("#2F5560"), FloorStyle = 0, Light = Palette.Hex("#A8F0E6") },
            new RoomDef { Id = RoomId.Greenhouse, Name = "INVERNADERO", Rect = new RectInt(1, 1, 10, 7), Floor = Palette.Hex("#3A4A3A"), FloorStyle = 1, Light = Palette.Hex("#E9D2FF") },
            new RoomDef { Id = RoomId.Engine, Name = "SALA DE MÁQUINAS", Rect = new RectInt(15, 1, 11, 7), Floor = Palette.Hex("#2A3038"), FloorStyle = 1, Light = Palette.Hex("#FFD9A8") },
            new RoomDef { Id = RoomId.Shelter, Name = "REFUGIO", Rect = new RectInt(30, 1, 10, 7), Floor = Palette.Hex("#3E3732"), FloorStyle = 0, Light = Palette.Hex("#FFE7C2") },
        };

        public static readonly RectInt[] Corridors =
        {
            new RectInt(11, 12, 4, 3), new RectInt(26, 12, 4, 3),   // superiores
            new RectInt(11, 3, 4, 3), new RectInt(26, 3, 4, 3),     // inferiores
            new RectInt(4, 8, 3, 2), new RectInt(19, 8, 3, 2), new RectInt(33, 8, 3, 2), // verticales
        };

        public static RoomDef Room(RoomId id)
        {
            foreach (var room in Rooms)
            {
                if (room.Id == id)
                    return room;
            }
            return Rooms[0];
        }

        public static RoomId RoomAt(Vector2 position)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
            foreach (var room in Rooms)
            {
                if (room.Rect.Contains(cell))
                    return room.Id;
            }
            return RoomId.Corridor;
        }

        public static bool[,] FloorGrid()
        {
            var grid = new bool[Width, Height];
            foreach (var room in Rooms)
                Fill(grid, room.Rect);
            foreach (var corridor in Corridors)
                Fill(grid, corridor);
            return grid;
        }

        static void Fill(bool[,] grid, RectInt rect)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
                for (int y = rect.yMin; y < rect.yMax; y++)
                    grid[x, y] = true;
        }

        /// <summary>Máquinas: tipo, habitación, posición local (desde la esquina inferior izquierda) e índice.</summary>
        public static IEnumerable<(MachineKind kind, RoomId room, Vector2 local, int index)> Machines()
        {
            yield return (MachineKind.ControlTerminal, RoomId.Control, new Vector2(4.2f, 5.6f), 0);
            yield return (MachineKind.CommsConsole, RoomId.Control, new Vector2(8.6f, 5.6f), 0);
            yield return (MachineKind.OxygenGenerator, RoomId.LifeSupport, new Vector2(2.6f, 5.0f), 0);
            yield return (MachineKind.WaterRecycler, RoomId.LifeSupport, new Vector2(7.4f, 5.0f), 0);
            yield return (MachineKind.CropBed, RoomId.Greenhouse, new Vector2(2.3f, 3.3f), 0);
            yield return (MachineKind.CropBed, RoomId.Greenhouse, new Vector2(5.0f, 3.3f), 1);
            yield return (MachineKind.CropBed, RoomId.Greenhouse, new Vector2(7.7f, 3.3f), 2);
            yield return (MachineKind.GreenhouseLights, RoomId.Greenhouse, new Vector2(8.3f, 5.9f), 0);
            yield return (MachineKind.ResearchBench, RoomId.Lab, new Vector2(5.0f, 5.4f), 0);
            yield return (MachineKind.BatteryBank, RoomId.Engine, new Vector2(3.4f, 5.4f), 0);
            yield return (MachineKind.PowerPanel, RoomId.Engine, new Vector2(8.6f, 5.4f), 0);
            yield return (MachineKind.ShelterConsole, RoomId.Shelter, new Vector2(4.6f, 5.4f), 0);
            yield return (MachineKind.RadiationMonitor, RoomId.Shelter, new Vector2(8.2f, 5.6f), 0);
        }

        /// <summary>Puntos de paso para la tripulación (centros de sala y de pasillo) y sus conexiones.</summary>
        public static readonly Vector2[] Waypoints =
        {
            new Vector2(6, 13.5f), new Vector2(20.5f, 13.5f), new Vector2(35, 13.5f),  // 0 Lab, 1 Control, 2 Soporte
            new Vector2(6, 4.5f), new Vector2(20.5f, 4.5f), new Vector2(35, 4.5f),     // 3 Invernadero, 4 Máquinas, 5 Refugio
            new Vector2(13, 13.5f), new Vector2(28, 13.5f), new Vector2(13, 4.5f), new Vector2(28, 4.5f), // 6-9 pasillos horizontales
            new Vector2(5.5f, 9), new Vector2(20.5f, 9), new Vector2(34.5f, 9),        // 10-12 pasillos verticales
        };

        public static readonly int[][] WaypointLinks =
        {
            new[] { 6, 10 }, new[] { 6, 7, 11 }, new[] { 7, 12 }, new[] { 8, 10 }, new[] { 8, 9, 11 }, new[] { 9, 12 },
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 3, 4 }, new[] { 4, 5 }, new[] { 0, 3 }, new[] { 1, 4 }, new[] { 2, 5 },
        };

        public static int WaypointOf(RoomId room)
        {
            switch (room)
            {
                case RoomId.Lab: return 0;
                case RoomId.Control: return 1;
                case RoomId.LifeSupport: return 2;
                case RoomId.Greenhouse: return 3;
                case RoomId.Engine: return 4;
                default: return 5;
            }
        }
    }
}
