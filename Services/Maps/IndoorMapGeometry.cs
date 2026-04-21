using System.Globalization;

namespace Services.Maps
{
    public sealed record ResolvedMapZone(
        string LayerId,
        string LayerLabel,
        string ZoneId,
        string ZoneLabel,
        string ZoneKind);

    public static class IndoorMapGeometry
    {
        public const double MapWidth = 1000d;
        public const double MapHeight = 700d;
        public const double CodecBaseLat = 42.1402;
        public const double CodecBaseLng = 24.7444;
        public const double CodecLayerGapLat = 0.0022;
        public const double CodecLayerGapLng = 0.0022;
        public const double CodecSpanLat = 0.0014;
        public const double CodecSpanLng = 0.0022;

        private static readonly string[] Layers =
        [
            "campus",
            "main:1",
            "main:2",
            "main:3",
            "main:4",
            "small:1",
            "small:2",
            "small:3"
        ];

        private static readonly Rect SmallHull = new(260d, 120d, 330d, 460d);
        private static readonly Rect CampusSmallBuildingRect = new(617.42d, 464.32d, 113.23d, 97.50d);

        private static readonly Point[] CampusOuterPolygon =
        [
            new(250.48d, 33.44d),
            new(719.11d, 33.44d),
            new(719.11d, 464.32d),
            new(760d, 464.32d),
            new(760d, 663.52d),
            new(528.31d, 663.52d),
            new(528.31d, 464.32d),
            new(250.48d, 464.32d)
        ];

        private static readonly Point[] CampusMainPolygon =
        [
            new(279.84d, 65.94d),
            new(324.53d, 65.94d),
            new(324.53d, 102.92d),
            new(342.25d, 102.92d),
            new(342.25d, 108.32d),
            new(447.05d, 108.32d),
            new(447.05d, 85.97d),
            new(476.72d, 85.97d),
            new(476.72d, 69.02d),
            new(583.83d, 69.02d),
            new(583.83d, 101d),
            new(542.60d, 101d),
            new(542.60d, 124.89d),
            new(469.79d, 124.89d),
            new(469.79d, 151.47d),
            new(334.94d, 151.47d),
            new(334.94d, 394.97d),
            new(506.77d, 394.97d),
            new(506.77d, 460.85d),
            new(322.60d, 460.85d),
            new(322.60d, 394.97d),
            new(292.56d, 394.97d),
            new(292.56d, 130.66d),
            new(279.84d, 130.66d)
        ];

        private static readonly IReadOnlyDictionary<int, Point[]> MainHullPolygons = new Dictionary<int, Point[]>
        {
            [1] =
            [
                new(40d, 110d), new(136d, 110d), new(136d, 206d), new(158d, 206d), new(158d, 234d), new(252d, 234d),
                new(252d, 168d), new(558d, 168d), new(558d, 242d), new(470d, 242d), new(470d, 338d), new(220d, 338d),
                new(220d, 618d), new(600d, 618d), new(600d, 692d), new(220d, 692d), new(220d, 658d), new(96d, 658d),
                new(96d, 270d), new(40d, 270d)
            ],
            [2] =
            [
                new(22d, 110d), new(158d, 110d), new(158d, 234d), new(480d, 234d), new(480d, 338d), new(220d, 338d),
                new(220d, 692d), new(96d, 692d), new(96d, 270d), new(22d, 270d)
            ],
            [3] =
            [
                new(92d, 132d), new(220d, 132d), new(220d, 268d), new(254d, 268d), new(254d, 676d), new(134d, 676d),
                new(134d, 298d), new(92d, 298d)
            ],
            [4] =
            [
                new(92d, 132d), new(220d, 132d), new(220d, 268d), new(254d, 268d), new(254d, 676d), new(134d, 676d),
                new(134d, 298d), new(92d, 298d)
            ]
        };

        private static readonly IReadOnlyDictionary<string, ZoneDef[]> Zones = new Dictionary<string, ZoneDef[]>
        {
            ["campus"] =
            [
                new("court", 594.35d, 220.05d, 125.81d, 80.73d, "special"),
                new("small-court", 627.90d, 573.35d, 109.03d, 68.15d, "special"),
                new("annex-yard", 592.26d, 464.32d, 138.39d, 199.19d, "zone"),
                new("north-walk", 250.48d, 33.44d, 468.63d, 18.87d, "zone"),
                new("west-garden", 250.48d, 52.31d, 30.40d, 412.02d, "zone"),
                new("north-garden", 368.95d, 69.08d, 39.84d, 68.15d, "zone"),
                new("east-garden", 528.31d, 174.97d, 45.08d, 167.74d, "zone"),
                new("south-east-garden", 604.84d, 349d, 114.27d, 93.31d, "zone"),
                new("south-garden", 528.31d, 464.32d, 62.90d, 199.19d, "zone"),
                new("east-border", 719.11d, 464.32d, 40.89d, 199.19d, "zone"),
                new("south-walk", 528.31d, 644.65d, 231.69d, 18.87d, "zone")
            ],
            ["main:1"] =
            [
                new("COR-V", 96d, 270d, 62d, 348d, "zone"),
                new("120", 40d, 110d, 56d, 96d, "class"),
                new("WC-1W", 40d, 206d, 56d, 32d, "zone"),
                new("WC-1M", 40d, 238d, 56d, 32d, "zone"),
                new("119", 96d, 110d, 40d, 52d, "class"),
                new("MED", 96d, 162d, 40d, 44d, "special"),
                new("FVS1-M", 252d, 168d, 58d, 74d, "zone"),
                new("FVS1", 310d, 168d, 190d, 74d, "special"),
                new("FVS1-W", 500d, 168d, 58d, 74d, "zone"),
                new("101", 158d, 338d, 62d, 56d, "class"),
                new("102", 158d, 394d, 62d, 56d, "class"),
                new("103", 158d, 450d, 62d, 56d, "class"),
                new("104", 158d, 506d, 62d, 56d, "class"),
                new("105", 158d, 562d, 62d, 56d, "class"),
                new("106", 96d, 618d, 124d, 40d, "class"),
                new("DINING", 220d, 618d, 380d, 74d, "special"),
                new("LOBBY", 220d, 242d, 250d, 96d, "special"),
                new("STAIR-1A", 158d, 234d, 62d, 104d, "zone")
            ],
            ["main:2"] =
            [
                new("COR-V", 96d, 338d, 62d, 320d, "zone"),
                new("209", 22d, 110d, 74d, 44d, "class"),
                new("210", 22d, 154d, 74d, 44d, "class"),
                new("WC-2W-A", 22d, 198d, 74d, 36d, "zone"),
                new("WC-2M-A", 22d, 234d, 74d, 36d, "zone"),
                new("207", 96d, 110d, 62d, 124d, "class"),
                new("OPEN-2A", 96d, 234d, 62d, 104d, "zone"),
                new("WC-2W-B", 220d, 234d, 130d, 52d, "zone"),
                new("WC-2M-B", 350d, 234d, 130d, 52d, "zone"),
                new("COR-T", 220d, 286d, 260d, 52d, "zone"),
                new("201", 158d, 338d, 62d, 56d, "class"),
                new("202", 158d, 394d, 62d, 56d, "class"),
                new("203", 158d, 450d, 62d, 56d, "class"),
                new("204", 158d, 506d, 62d, 56d, "class"),
                new("205", 158d, 562d, 62d, 48d, "class"),
                new("206", 158d, 610d, 62d, 48d, "class"),
                new("FVS2", 332d, 132d, 184d, 92d, "special"),
                new("STAIR-2A", 158d, 234d, 62d, 104d, "zone"),
                new("STAIR-2B", 96d, 658d, 124d, 34d, "zone")
            ],
            ["main:3"] =
            [
                new("COR-V", 134d, 334d, 60d, 294d, "zone"),
                new("301", 194d, 334d, 60d, 42d, "class"),
                new("302", 194d, 376d, 60d, 42d, "class"),
                new("303", 194d, 418d, 60d, 42d, "class"),
                new("304", 194d, 460d, 60d, 42d, "class"),
                new("305", 194d, 502d, 60d, 42d, "class"),
                new("306", 194d, 544d, 60d, 42d, "class"),
                new("307", 166d, 222d, 54d, 46d, "class"),
                new("308", 166d, 132d, 54d, 50d, "class"),
                new("309", 194d, 586d, 60d, 42d, "class"),
                new("310", 166d, 182d, 54d, 40d, "class"),
                new("LIB", 92d, 132d, 42d, 56d, "special"),
                new("SEC", 92d, 188d, 42d, 42d, "special"),
                new("WC-3M", 92d, 230d, 42d, 34d, "zone"),
                new("WC-3W", 92d, 264d, 42d, 34d, "zone"),
                new("OPEN-3C", 220d, 268d, 34d, 66d, "zone"),
                new("STAIR-3B", 134d, 628d, 120d, 48d, "zone")
            ],
            ["main:4"] =
            [
                new("COR-V", 134d, 334d, 60d, 294d, "zone"),
                new("401", 194d, 334d, 60d, 42d, "class"),
                new("402", 194d, 376d, 60d, 42d, "class"),
                new("403", 194d, 418d, 60d, 42d, "class"),
                new("404", 194d, 460d, 60d, 42d, "class"),
                new("405", 194d, 502d, 60d, 42d, "class"),
                new("406", 194d, 544d, 60d, 42d, "class"),
                new("407", 166d, 222d, 54d, 46d, "class"),
                new("408", 166d, 132d, 54d, 50d, "class"),
                new("410", 92d, 188d, 42d, 42d, "class"),
                new("411", 92d, 132d, 42d, 56d, "class"),
                new("LABP", 166d, 182d, 54d, 40d, "lab"),
                new("WC-4M", 92d, 230d, 42d, 34d, "zone"),
                new("WC-4W", 92d, 264d, 42d, 34d, "zone"),
                new("OPEN-4C", 220d, 268d, 34d, 66d, "zone"),
                new("HIST", 194d, 586d, 60d, 42d, "special"),
                new("STAIR-4B", 134d, 628d, 120d, 48d, "zone")
            ],
            ["small:1"] =
            [
                new("PHY", 260d, 120d, 110d, 100d, "lab"),
                new("BIO", 260d, 220d, 110d, 96d, "lab"),
                new("CHEM", 260d, 316d, 110d, 92d, "lab"),
                new("WC-S1W", 260d, 408d, 110d, 66d, "zone"),
                new("WC-S1M", 260d, 474d, 110d, 106d, "zone"),
                new("COR-S1", 370d, 120d, 110d, 460d, "zone"),
                new("113", 480d, 120d, 110d, 92d, "class"),
                new("114", 480d, 212d, 110d, 96d, "class"),
                new("SERV", 480d, 308d, 110d, 114d, "special"),
                new("STAIR-S1", 480d, 422d, 110d, 158d, "zone")
            ],
            ["small:2"] =
            [
                new("211", 260d, 120d, 110d, 88d, "class"),
                new("212", 260d, 208d, 110d, 188d, "class"),
                new("WC-S2M", 260d, 396d, 110d, 70d, "zone"),
                new("WC-S2W", 260d, 466d, 110d, 114d, "zone"),
                new("COR-S2", 370d, 120d, 110d, 460d, "zone"),
                new("213", 480d, 120d, 110d, 86d, "class"),
                new("214", 480d, 206d, 110d, 220d, "class"),
                new("STAIR-S2", 480d, 426d, 110d, 154d, "zone")
            ],
            ["small:3"] =
            [
                new("TECH", 260d, 120d, 110d, 138d, "special"),
                new("RESEARCH", 260d, 258d, 110d, 128d, "special"),
                new("WC-S3M", 260d, 386d, 110d, 70d, "zone"),
                new("WC-S3W", 260d, 456d, 110d, 124d, "zone"),
                new("COR-S3", 370d, 120d, 110d, 460d, "zone"),
                new("315", 370d, 120d, 110d, 76d, "class"),
                new("313", 480d, 120d, 110d, 134d, "class"),
                new("314", 480d, 254d, 110d, 172d, "class"),
                new("STAIR-S3", 480d, 426d, 110d, 154d, "zone")
            ]
        };

        public static (double Latitude, double Longitude) EncodeLayerPoint(string layerId, double x, double y)
        {
            var index = Array.IndexOf(Layers, layerId);
            if (index < 0)
            {
                index = 0;
            }

            var baseLat = CodecBaseLat + index * CodecLayerGapLat;
            var baseLng = CodecBaseLng + index * CodecLayerGapLng;
            var normalizedX = Math.Clamp(x / MapWidth, 0d, 1d);
            var normalizedY = Math.Clamp(y / MapHeight, 0d, 1d);

            return
            (
                baseLat + (0.5d - normalizedY) * CodecSpanLat,
                baseLng + (normalizedX - 0.5d) * CodecSpanLng
            );
        }

        public static bool TryResolveZone(double latitude, double longitude, out ResolvedMapZone? zone)
        {
            zone = null;

            if (!TryDecodeLayerPoint(latitude, longitude, out var layerId, out var x, out var y))
            {
                return false;
            }

            zone = ResolveZone(layerId, x, y);
            return zone is not null;
        }

        public static bool TryDecodeLayerPoint(double latitude, double longitude, out string layerId, out double x, out double y)
        {
            layerId = Layers[0];
            x = 0d;
            y = 0d;

            if (double.IsNaN(latitude) || double.IsInfinity(latitude) || double.IsNaN(longitude) || double.IsInfinity(longitude))
            {
                return false;
            }

            var approxIndex = (int)Math.Round((latitude - CodecBaseLat) / CodecLayerGapLat, MidpointRounding.AwayFromZero);
            var index = Math.Clamp(approxIndex, 0, Layers.Length - 1);
            layerId = Layers[index];

            var baseLat = CodecBaseLat + index * CodecLayerGapLat;
            var baseLng = CodecBaseLng + index * CodecLayerGapLng;
            var normalizedY = 0.5d - (latitude - baseLat) / CodecSpanLat;
            var normalizedX = (longitude - baseLng) / CodecSpanLng + 0.5d;
            x = normalizedX * MapWidth;
            y = normalizedY * MapHeight;

            return x >= -120d && x <= MapWidth + 120d && y >= -120d && y <= MapHeight + 120d;
        }

        private static ResolvedMapZone? ResolveZone(string layerId, double x, double y)
        {
            if (layerId == "campus")
            {
                return ResolveCampusZone(x, y);
            }

            if (Zones.TryGetValue(layerId, out var zones))
            {
                var zone = zones.FirstOrDefault(item => Contains(item, x, y));
                if (zone is not null)
                {
                    return new ResolvedMapZone(
                        layerId,
                        GetLayerLabel(layerId),
                        zone.Id,
                        GetZoneLabel(layerId, zone.Id, zone.Kind),
                        zone.Kind);
                }
            }

            var parts = layerId.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor))
            {
                return null;
            }

            if (parts[0] == "main" && MainHullPolygons.TryGetValue(floor, out var polygon) && PointInPolygon(x, y, polygon))
            {
                return new ResolvedMapZone(
                    layerId,
                    GetLayerLabel(layerId),
                    $"COR-FALLBACK-{layerId}",
                    "Коридор",
                    "zone");
            }

            if (parts[0] == "small" && Contains(SmallHull, x, y))
            {
                return new ResolvedMapZone(
                    layerId,
                    GetLayerLabel(layerId),
                    $"COR-FALLBACK-{layerId}",
                    "Коридор",
                    "zone");
            }

            return null;
        }

        private static ResolvedMapZone? ResolveCampusZone(double x, double y)
        {
            if (!PointInPolygon(x, y, CampusOuterPolygon))
            {
                return null;
            }

            if (PointInPolygon(x, y, CampusMainPolygon) || Contains(CampusSmallBuildingRect, x, y))
            {
                return null;
            }

            if (Zones.TryGetValue("campus", out var zones))
            {
                var zone = zones.FirstOrDefault(item => Contains(item, x, y));
                if (zone is not null)
                {
                    return new ResolvedMapZone(
                        "campus",
                        GetLayerLabel("campus"),
                        zone.Id,
                        GetZoneLabel("campus", zone.Id, zone.Kind),
                        zone.Kind);
                }
            }

            return new ResolvedMapZone(
                "campus",
                GetLayerLabel("campus"),
                "CAMPUS-YARD",
                "Дворна зона",
                "zone");
        }

        public static string GetLayerLabel(string layerId)
        {
            return layerId switch
            {
                "campus" => "Кампус · МГ \"Академик Кирил Попов\" - Пловдив",
                "main:1" => "Голяма сграда · Етаж 1",
                "main:2" => "Голяма сграда · Етаж 2",
                "main:3" => "Голяма сграда · Етаж 3",
                "main:4" => "Голяма сграда · Етаж 4",
                "small:1" => "Малка сграда · Етаж 1",
                "small:2" => "Малка сграда · Етаж 2",
                "small:3" => "Малка сграда · Етаж 3",
                _ => layerId
            };
        }

        public static string GetZoneLabel(string layerId, string zoneId, string zoneKind)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return zoneKind == "zone" ? "Коридор" : "Зона";
            }

            if (zoneId.StartsWith("COR-FALLBACK-", StringComparison.OrdinalIgnoreCase))
            {
                return "Коридор";
            }

            if (zoneId.StartsWith("WC-", StringComparison.OrdinalIgnoreCase))
            {
                return zoneId.Contains('W', StringComparison.OrdinalIgnoreCase) ? "Тоалетна - жени" : "Тоалетна - мъже";
            }

            return zoneId switch
            {
                "MED" => "Лекарски кабинет",
                "FVS1" => "ФВС салон 1",
                "FVS1-M" => "Съблекалня - мъже",
                "FVS1-W" => "Съблекалня - жени",
                "FVS2" => "ФВС салон 2",
                "DINING" => "Столова",
                "LOBBY" => "Лоби",
                "STAIR-1A" or "STAIR-2A" or "STAIR-2B" or "STAIR-3B" or "STAIR-4B" or "STAIR-S1" or "STAIR-S2" or "STAIR-S3" => "Стълбище",
                "LIB" => "Библиотека",
                "SEC" => "Секретариат",
                "HIST" => "Клуб на историка",
                "LABP" => "Лаборатория по програмиране",
                "PHY" => "Лаборатория по физика",
                "BIO" => "Лаборатория по биология",
                "CHEM" => "Лаборатория по химия",
                "SERV" => "Обслужващ персонал",
                "TECH" => "Технологии и роботика",
                "RESEARCH" => "Изследвания и проучвания",
                "court" => "Голямо игрище",
                "small-court" => "Малко игрище",
                "annex-yard" => "Дворна зона",
                "north-walk" => "Северна алея",
                "west-garden" => "Западна зелена зона",
                "north-garden" => "Северна зелена зона",
                "east-garden" => "Източна зелена зона",
                "south-east-garden" => "Югоизточна зелена зона",
                "south-garden" => "Южна зелена зона",
                "east-border" => "Източна алея",
                "south-walk" => "Южна алея",
                "CAMPUS-YARD" => "Дворна зона",
                _ when zoneId.All(char.IsDigit) => zoneId,
                _ when zoneId.StartsWith("OPEN-", StringComparison.OrdinalIgnoreCase) => "Обща зона",
                _ => zoneId
            };
        }

        private static bool Contains(ZoneDef zone, double x, double y) => x >= zone.X && x <= zone.X + zone.Width && y >= zone.Y && y <= zone.Y + zone.Height;

        private static bool Contains(Rect rect, double x, double y) => x >= rect.X && x <= rect.X + rect.Width && y >= rect.Y && y <= rect.Y + rect.Height;

        private static bool PointInPolygon(double x, double y, IReadOnlyList<Point> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var xi = polygon[i].X;
                var yi = polygon[i].Y;
                var xj = polygon[j].X;
                var yj = polygon[j].Y;
                var intersect = yi > y != yj > y && x < ((xj - xi) * (y - yi)) / ((yj - yi == 0d) ? 1e-9 : (yj - yi)) + xi;
                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private sealed record ZoneDef(string Id, double X, double Y, double Width, double Height, string Kind);
        private sealed record Rect(double X, double Y, double Width, double Height);
        private sealed record Point(double X, double Y);
    }
}
