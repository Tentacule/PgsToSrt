using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using SixLabors.ImageSharp;

namespace PgsToSrt.BluRaySup;

public static class BluRaySupParserImageSharp
{
    public static bool BluRaySupForceMergeAll { get; set; }
    public static bool BluRaySupSkipMerge { get; set; }

    public static List<PcsData> ParseBluRaySup(string fileName, StringBuilder log)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var lastPalettes = new Dictionary<int, List<PaletteInfo>>();
        var bitmapObjects = new Dictionary<int, List<OdsData>>();
        return ParseBluRaySup(stream, log, false, lastPalettes, bitmapObjects);
    }

    public static List<PcsData> ParseBluRaySupFromMatroska(
        MatroskaTrackInfo matroskaSubtitleInfo,
        MatroskaFile matroska)
    {
        var subtitle = matroska.GetSubtitle(matroskaSubtitleInfo.TrackNumber, null);
        var raySupFromMatroska = new List<PcsData>();
        var log = new StringBuilder();
        var ms = new MemoryStream();
        var lastPalettes = new Dictionary<int, List<PaletteInfo>>();
        var bitmapObjects = new Dictionary<int, List<OdsData>>();
        foreach (var matroskaSubtitle in subtitle)
        {
            var data = matroskaSubtitle.GetData(matroskaSubtitleInfo);
            if (data is {Length: > 2})
            {
                ms.Write(data, 0, data.Length);
                if (ContainsBluRayStartSegment(data))
                {
                    if (raySupFromMatroska.Count > 0 && raySupFromMatroska[raySupFromMatroska.Count - 1].StartTime == raySupFromMatroska[raySupFromMatroska.Count - 1].EndTime)
                        raySupFromMatroska[raySupFromMatroska.Count - 1].EndTime = (long) ((matroskaSubtitle.Start - 1L) * 90.0);
                    ms.Position = 0L;
                    foreach (var pcsData in ParseBluRaySup(ms, log, true, lastPalettes, bitmapObjects))
                    {
                        pcsData.StartTime = (long) ((matroskaSubtitle.Start - 1L) * 90.0);
                        pcsData.EndTime = (long) ((matroskaSubtitle.End - 1L) * 90.0);
                        raySupFromMatroska.Add(pcsData);
                        if (raySupFromMatroska.Count > 1 && subtitle[raySupFromMatroska.Count - 2].End > subtitle[raySupFromMatroska.Count - 1].Start)
                            raySupFromMatroska[raySupFromMatroska.Count - 2].EndTime = raySupFromMatroska[raySupFromMatroska.Count - 1].StartTime - 1L;
                    }

                    ms = new MemoryStream();
                }
            }
            else if (raySupFromMatroska.Count > 0)
            {
                PcsData pcsData = raySupFromMatroska[raySupFromMatroska.Count - 1];
                if (pcsData.StartTime == pcsData.EndTime)
                {
                    pcsData.EndTime = (long) ((matroskaSubtitle.Start - 1L) * 90.0);
                    if (pcsData.EndTime - pcsData.StartTime > 1000000L)
                        pcsData.EndTime = pcsData.StartTime;
                }
            }
        }

        return raySupFromMatroska;
    }

    private static bool ContainsBluRayStartSegment(byte[] buffer)
    {
        int num;
        for (int index = 0; index + 3 <= buffer.Length; index += num)
        {
            if (buffer[index] == 128)
                return true;
            num = BigEndianInt16(buffer, index + 1) + 3;
        }

        return false;
    }

    private static SupSegment ParseSegmentHeader(byte[] buffer, StringBuilder log)
    {
        var segmentHeader = new SupSegment();
        if (buffer[0] == 80 && buffer[1] == 71)
        {
            segmentHeader.PtsTimestamp = BigEndianInt32(buffer, 2);
            segmentHeader.Type = buffer[10];
            segmentHeader.Size = BigEndianInt16(buffer, 11);
        }

        return segmentHeader;
    }

    private static SupSegment ParseSegmentHeaderFromMatroska(byte[] buffer)
    {
        return new SupSegment()
        {
            Type = buffer[0],
            Size = BigEndianInt16(buffer, 1)
        };
    }

    private static PcsObject ParsePcs(byte[] buffer, int offset)
    {
        return new PcsObject()
        {
            ObjectId = BigEndianInt16(buffer, 11 + offset),
            WindowId = buffer[13 + offset],
            IsForced = (buffer[14 + offset] & 64) == 64,
            Origin = new Point(BigEndianInt16(buffer, 15 + offset), BigEndianInt16(buffer, 17 + offset))
        };
    }

    private static PcsData ParsePicture(
        byte[] buffer,
        SupSegment segment)
    {
        if (buffer.Length < 11)
            return new PcsData()
            {
                CompositionState = CompositionState.Invalid
            };
        var stringBuilder = new StringBuilder();
        var picture = new PcsData()
        {
            Size = new Size(BigEndianInt16(buffer, 0), BigEndianInt16(buffer, 2)),
            FramesPerSecondType = buffer[4],
            CompNum = BigEndianInt16(buffer, 5),
            CompositionState = GetCompositionState(buffer[7]),
            StartTime = segment.PtsTimestamp,
            PaletteUpdate = buffer[8] == 128,
            PaletteId = buffer[9]
        };
        var num = buffer[10];
        stringBuilder.Append($"CompNum: {picture.CompNum}, Pts: {ToolBox.PtsToTimeString(picture.StartTime)}, State: {(object) picture.CompositionState}, PalUpdate: {(object) picture.PaletteUpdate}, PalId {(object) picture.PaletteId}");
        if (picture.CompositionState == CompositionState.Invalid)
        {
            stringBuilder.Append("Illegal composition state Invalid");
        }
        else
        {
            var offset = 0;
            picture.PcsObjects = new List<PcsObject>();
            for (var index = 0; index < num; ++index)
            {
                var pcs = ParsePcs(buffer, offset);
                picture.PcsObjects.Add(pcs);
                stringBuilder.AppendLine();
                stringBuilder.Append($"ObjId: {pcs.ObjectId}, WinId: {pcs.WindowId}, Forced: {pcs.IsForced}, X: {pcs.Origin.X}, Y: {pcs.Origin.Y}");
                offset += 8;
            }
        }

        picture.Message = stringBuilder.ToString();
        return picture;
    }

    private static bool CompletePcs(
        PcsData pcs,
        Dictionary<int, List<OdsData>> bitmapObjects,
        Dictionary<int, List<PaletteInfo>> palettes)
    {
        if (pcs?.PcsObjects == null || palettes == null)
            return false;
        if (pcs.PcsObjects.Count == 0)
            return true;
        if (!palettes.ContainsKey(pcs.PaletteId))
            return false;
        pcs.PaletteInfos = new List<PaletteInfo>(palettes[pcs.PaletteId]);
        pcs.BitmapObjects = new List<List<OdsData>>();
        var flag = false;
        for (var index = 0; index < pcs.PcsObjects.Count; ++index)
        {
            var objectId = pcs.PcsObjects[index].ObjectId;
            if (bitmapObjects.ContainsKey(objectId))
            {
                pcs.BitmapObjects.Add(bitmapObjects[objectId]);
                flag = true;
            }
        }

        return flag;
    }

    private static PdsData ParsePds(
        byte[] buffer,
        SupSegment segment)
    {
        int num1 = buffer[0];
        int num2 = buffer[1];
        PaletteInfo paletteInfo = new PaletteInfo()
        {
            PaletteSize = (segment.Size - 2) / 5
        };
        if (paletteInfo.PaletteSize <= 0)
            return new PdsData()
            {
                Message = "Empty palette"
            };
        paletteInfo.PaletteBuffer = new byte[paletteInfo.PaletteSize * 5];
        Buffer.BlockCopy(buffer, 2, paletteInfo.PaletteBuffer, 0, paletteInfo.PaletteSize * 5);
        return new PdsData()
        {
            Message = "PalId: " + num1 + ", update: " + num2 + ", " + paletteInfo.PaletteSize + " entries",
            PaletteId = num1,
            PaletteVersion = num2,
            PaletteInfo = paletteInfo
        };
    }

    private static OdsData ParseOds(
        byte[] buffer,
        SupSegment segment,
        bool forceFirst)
    {
        var num1 = BigEndianInt16(buffer, 0);
        var num2 = buffer[2];
        var num3 = buffer[3];
        var flag1 = (num3 & 128) == 128 | forceFirst;
        var flag2 = (num3 & 64) == 64;
        var imageObjectFragment = new ImageObjectFragment();
        if (flag1)
        {
            var width = BigEndianInt16(buffer, 7);
            var height = BigEndianInt16(buffer, 9);
            imageObjectFragment.ImagePacketSize = segment.Size - 11;
            imageObjectFragment.ImageBuffer = new byte[imageObjectFragment.ImagePacketSize];
            Buffer.BlockCopy(buffer, 11, imageObjectFragment.ImageBuffer, 0, imageObjectFragment.ImagePacketSize);
            return new OdsData()
            {
                IsFirst = true,
                Size = new Size(width, height),
                ObjectId = num1,
                ObjectVersion = num2,
                Fragment = imageObjectFragment,
                Message = "ObjId: " + num1 + ", ver: " + num2 + ", seq: first" + (flag2 ? "/" : "") + (flag2 ? "last" : "") + ", width: " + width.ToString() + ", height: " + height.ToString()
            };
        }

        imageObjectFragment.ImagePacketSize = segment.Size - 4;
        imageObjectFragment.ImageBuffer = new byte[imageObjectFragment.ImagePacketSize];
        Buffer.BlockCopy(buffer, 4, imageObjectFragment.ImageBuffer, 0, imageObjectFragment.ImagePacketSize);
        return new OdsData()
        {
            IsFirst = false,
            ObjectId = num1,
            ObjectVersion = num2,
            Fragment = imageObjectFragment,
            Message = "Continued ObjId: " + num1 + ", ver: " + num2 + ", seq: " + (flag2 ? "last" : "")
        };
    }

    private static List<PcsData> ParseBluRaySup(
        Stream ms,
        StringBuilder log,
        bool fromMatroskaFile,
        Dictionary<int, List<PaletteInfo>> lastPalettes,
        Dictionary<int, List<OdsData>> bitmapObjects)
    {
        var num1 = ms.Position;
        var num2 = 0;
        var dictionary = new Dictionary<int, List<PaletteInfo>>();
        var forceFirst = true;
        var bluRaySup = new List<PcsData>();

        PcsData pcs1 = null;

        var buffer1 = fromMatroskaFile ? new byte[3] : new byte[13];
        while (ms.Read(buffer1, 0, buffer1.Length) == buffer1.Length)
        {
            var segment = fromMatroskaFile ? ParseSegmentHeaderFromMatroska(buffer1) : ParseSegmentHeader(buffer1, log);
            var num3 = num1 + buffer1.Length;
            try
            {
                byte[] buffer2 = new byte[segment.Size];
                if (ms.Read(buffer2, 0, buffer2.Length) >= buffer2.Length)
                {
                    switch (segment.Type)
                    {
                        case 20:
                            if (pcs1 != null)
                            {
                                PdsData pds = ParsePds(buffer2, segment);
                                if (pds.PaletteInfo != null)
                                {
                                    if (!dictionary.ContainsKey(pds.PaletteId))
                                        dictionary[pds.PaletteId] = new List<PaletteInfo>();
                                    else if (pcs1.PaletteUpdate)
                                        dictionary[pds.PaletteId].RemoveAt(dictionary[pds.PaletteId].Count - 1);
                                    dictionary[pds.PaletteId].Add(pds.PaletteInfo);
                                    break;
                                }

                                break;
                            }

                            break;
                        case 21:
                            if (pcs1 != null)
                            {
                                OdsData ods = ParseOds(buffer2, segment, forceFirst);
                                List<OdsData> odsDataList;
                                if (!pcs1.PaletteUpdate)
                                {
                                    if (ods.IsFirst)
                                    {
                                        odsDataList = new List<OdsData>()
                                        {
                                            ods
                                        };
                                        bitmapObjects[ods.ObjectId] = odsDataList;
                                    }
                                    else if (bitmapObjects.TryGetValue(ods.ObjectId, out odsDataList))
                                        odsDataList.Add(ods);
                                }

                                forceFirst = false;
                                break;
                            }

                            break;
                        case 22:
                            if (pcs1 != null && CompletePcs(pcs1, bitmapObjects, dictionary.Count > 0 ? dictionary : lastPalettes))
                                bluRaySup.Add(pcs1);
                            forceFirst = true;
                            PcsData picture = ParsePicture(buffer2, segment);
                            if (picture.StartTime > 0L && bluRaySup.Count > 0 && bluRaySup.Last().EndTime == 0L)
                                bluRaySup.Last().EndTime = picture.StartTime;
                            pcs1 = picture;
                            if (pcs1.CompositionState == CompositionState.EpochStart)
                            {
                                bitmapObjects.Clear();
                                dictionary.Clear();
                                break;
                            }

                            break;
                        case 23:
                            if (pcs1 != null)
                            {
                                int num4 = buffer2[0];
                                int num5 = 0;
                                for (int index = 0; index < num4; ++index)
                                {
                                    int num6 = buffer2[1 + num5];
                                    int num7 = BigEndianInt16(buffer2, 2 + num5);
                                    int num8 = BigEndianInt16(buffer2, 4 + num5);
                                    int num9 = BigEndianInt16(buffer2, 6 + num5);
                                    int num10 = BigEndianInt16(buffer2, 8 + num5);
                                    log.AppendLine(string.Format("WinId: {4}, X: {0}, Y: {1}, Width: {2}, Height: {3}", num7, num8, num9, num10, num6));
                                    num5 += 9;
                                }

                                break;
                            }

                            break;
                        case 128:
                            forceFirst = true;
                            if (pcs1 != null)
                            {
                                if (CompletePcs(pcs1, bitmapObjects, dictionary.Count > 0 ? dictionary : lastPalettes))
                                    bluRaySup.Add(pcs1);
                                pcs1 = null;
                                break;
                            }

                            break;
                    }
                }
                else
                    break;
            }
            catch (IndexOutOfRangeException ex)
            {
                log.Append($"Index of of range at pos {num3 - buffer1.Length}: {ex.StackTrace}");
            }

            num1 = num3 + segment.Size;
            ++num2;
        }

        if (pcs1 != null && CompletePcs(pcs1, bitmapObjects, dictionary.Count > 0 ? dictionary : lastPalettes))
            bluRaySup.Add(pcs1);
        for (int index = 1; index < bluRaySup.Count; ++index)
        {
            PcsData pcsData = bluRaySup[index - 1];
            if (pcsData.EndTime == 0L)
                pcsData.EndTime = bluRaySup[index].StartTime;
        }

        bluRaySup.RemoveAll((Predicate<PcsData>) (pcs => pcs.PcsObjects.Count == 0));
        foreach (PcsData pcsData in bluRaySup)
        {
            foreach (List<OdsData> bitmapObject in pcsData.BitmapObjects)
            {
                if (bitmapObject.Count > 1)
                {
                    int length = 0;
                    foreach (OdsData odsData in bitmapObject)
                        length += odsData.Fragment.ImagePacketSize;
                    byte[] dst = new byte[length];
                    int dstOffset = 0;
                    foreach (OdsData odsData in bitmapObject)
                    {
                        Buffer.BlockCopy(odsData.Fragment.ImageBuffer, 0, dst, dstOffset, odsData.Fragment.ImagePacketSize);
                        dstOffset += odsData.Fragment.ImagePacketSize;
                    }

                    bitmapObject[0].Fragment.ImageBuffer = dst;
                    bitmapObject[0].Fragment.ImagePacketSize = length;
                    while (bitmapObject.Count > 1)
                        bitmapObject.RemoveAt(1);
                }
            }
        }

        if (!BluRaySupSkipMerge || BluRaySupForceMergeAll)
        {
            var source1 = new List<DeleteIndex>();
            var deleteNo = 0;
            for (var pcsIndex = bluRaySup.Count - 1; pcsIndex > 0; pcsIndex--)
            {
                var pcsData1 = bluRaySup[pcsIndex];
                var pcsData2 = bluRaySup[pcsIndex - 1];
                if (Math.Abs(pcsData2.EndTime - pcsData1.StartTime) < 10L)
                {
                    var size = pcsData2.Size;
                    var width1 = size.Width;
                    size = pcsData1.Size;
                    var width2 = size.Width;
                    if (width1 == width2)
                    {
                        size = pcsData2.Size;
                        int height1 = size.Height;
                        size = pcsData1.Size;
                        int height2 = size.Height;
                        if (height1 == height2)
                        {
                            if (pcsData1.BitmapObjects.Count > 0 && pcsData1.BitmapObjects[0].Count > 0 && pcsData2.BitmapObjects.Count == pcsData1.BitmapObjects.Count && pcsData2.BitmapObjects[0].Count == pcsData1.BitmapObjects[0].Count)
                            {
                                var flag = true;
                                for (var index1 = 0; index1 < pcsData1.BitmapObjects.Count; ++index1)
                                {
                                    var bitmapObject1 = pcsData1.BitmapObjects[index1];
                                    var bitmapObject2 = pcsData2.BitmapObjects[index1];
                                    if (bitmapObject2.Count == bitmapObject1.Count)
                                    {
                                        for (var index2 = 0; index2 < bitmapObject1.Count; ++index2)
                                        {
                                            if (!ByteArraysEqual(bitmapObject1[index2].Fragment.ImageBuffer, bitmapObject2[index2].Fragment.ImageBuffer))
                                            {
                                                flag = false;
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        flag = false;
                                        break;
                                    }
                                }

                                if (flag)
                                {
                                    if (!source1.Any((Func<DeleteIndex, bool>) (p => p.Number == deleteNo && p.Index == pcsIndex - 1)))
                                        source1.Add(new DeleteIndex()
                                        {
                                            Number = deleteNo,
                                            Index = pcsIndex - 1
                                        });
                                    if (!source1.Any((Func<DeleteIndex, bool>) (p => p.Number == deleteNo && p.Index == pcsIndex)))
                                    {
                                        source1.Add(new DeleteIndex()
                                        {
                                            Number = deleteNo,
                                            Index = pcsIndex
                                        });
                                        //continue;
                                    }

                                    continue;
                                }

                                deleteNo++;
                                //continue;
                            }

                            continue;
                        }
                    }
                }

                deleteNo++;
            }

            var mergeCount = source1.GroupBy((Func<DeleteIndex, int>) (p => p.Number)).Count<IGrouping<int, DeleteIndex>>();
            foreach (IGrouping<int, DeleteIndex> source2 in source1.GroupBy((Func<DeleteIndex, int>) (p => p.Number)).OrderBy<IGrouping<int, DeleteIndex>, int>((Func<IGrouping<int, DeleteIndex>, int>) (p => p.Key)))
            {
                DeleteIndex[] array = source2.OrderByDescending((Func<DeleteIndex, int>) (p => p.Index)).ToArray<DeleteIndex>();
                int index = (int) Math.Round(source2.Count() / 2.0);
                DeleteIndex deleteIndex1 = array[index];
                if (QualifiesForMerge(array, bluRaySup, mergeCount))
                {
                    bluRaySup[deleteIndex1.Index].StartTime = bluRaySup[array.Last().Index].StartTime;
                    bluRaySup[deleteIndex1.Index].EndTime = bluRaySup[array.First().Index].EndTime;
                    foreach (DeleteIndex deleteIndex2 in (IEnumerable<DeleteIndex>) source2.OrderByDescending<DeleteIndex, int>((Func<DeleteIndex, int>) (p => p.Index)))
                    {
                        if (deleteIndex2 != deleteIndex1)
                            bluRaySup.RemoveAt(deleteIndex2.Index);
                    }
                }
            }
        }

        if (lastPalettes != null && dictionary.Count > 0)
        {
            lastPalettes.Clear();
            foreach (var keyValuePair in dictionary)
                lastPalettes.Add(keyValuePair.Key, keyValuePair.Value);
        }

        return bluRaySup;
    }

    private static bool QualifiesForMerge(
        IReadOnlyList<DeleteIndex> arr,
        IReadOnlyList<PcsData> pcsList,
        int mergeCount)
    {
        if (BluRaySupForceMergeAll || mergeCount < 3)
            return false;
        if (arr.Count != 2)
            return true;
        var pcs1 = pcsList[arr[0].Index];
        var pcs2 = pcsList[arr[1].Index];
        var num1 = pcs1.EndTimeCode.TotalMilliseconds - pcs1.StartTimeCode.TotalMilliseconds;
        var num2 = pcs2.EndTimeCode.TotalMilliseconds - pcs2.StartTimeCode.TotalMilliseconds;
        if (num1 < 400.0 || num2 < 400.0 || pcs1.PaletteInfos.Count > 2 || pcs2.PaletteInfos.Count > 2)
            return true;

        using var bitmap1 = pcs1.GetRgba32();
        using var bitmap2 = pcs2.GetRgba32();

        var transparentHeight = bitmap1.GetNonTransparentHeight();
        var transparentWidth = bitmap1.GetNonTransparentWidth();
        if (transparentHeight > 110 || transparentWidth > 300)
            return true;

        return bitmap1.IsEqualTo(bitmap2);
    }

    private static bool ByteArraysEqual(byte[] b1, byte[] b2)
    {
        if (b1 == b2)
            return true;
        if (b1 == null || b2 == null || b1.Length != b2.Length)
            return false;
        for (int index = 0; index < b1.Length; ++index)
        {
            if (b1[index] != b2[index])
                return false;
        }

        return true;
    }

    private static CompositionState GetCompositionState(byte type)
    {
        switch (type)
        {
            case 0:
                return CompositionState.Normal;
            case 64:
                return CompositionState.AcquPoint;
            case 128:
                return CompositionState.EpochStart;
            case 192:
                return CompositionState.EpochContinue;
            default:
                return CompositionState.Invalid;
        }
    }

    private static int BigEndianInt16(byte[] buffer, int index)
    {
        return buffer.Length < 2 ? 0 : buffer[index + 1] | buffer[index] << 8;
    }

    private static uint BigEndianInt32(byte[] buffer, int index)
    {
        return buffer.Length < 4 ? 0U : (uint) (buffer[index + 3] + (buffer[index + 2] << 8) + (buffer[index + 1] << 16) + (buffer[index] << 24));
    }

    private class SupSegment
    {
        public int Type { get; set; }

        public int Size { get; set; }

        public long PtsTimestamp { get; set; }
    }

    public class PcsObject
    {
        public int ObjectId { get; init; }

        public int WindowId { get; init; }

        public bool IsForced { get; init; }

        public Point Origin { get; init; }
    }

    public static BluRaySupPalette DecodePalette(IList<PaletteInfo> paletteInfos)
    {
        var bluRaySupPalette = new BluRaySupPalette(256);
        if (paletteInfos.Count == 0)
            return bluRaySupPalette;
        var paletteInfo = paletteInfos[paletteInfos.Count - 1];
        var flag = false;
        var index1 = 0;
        for (var index2 = 0; index2 < paletteInfo.PaletteSize; ++index2)
        {
            var index3 = paletteInfo.PaletteBuffer[index1];
            int num1;
            var yn = paletteInfo.PaletteBuffer[num1 = index1 + 1];
            int num2;
            var crn = paletteInfo.PaletteBuffer[num2 = num1 + 1];
            int num3;
            var cbn = paletteInfo.PaletteBuffer[num3 = num2 + 1];
            int num4;
            var alpha1 = paletteInfo.PaletteBuffer[num4 = num3 + 1];
            var alpha2 = bluRaySupPalette.GetAlpha(index3);
            if (alpha1 >= alpha2)
            {
                if (alpha1 < 14)
                {
                    yn = 16;
                    crn = 128;
                    cbn = 128;
                }

                bluRaySupPalette.SetAlpha(index3, alpha1);
            }
            else
                flag = true;

            bluRaySupPalette.SetYCbCr(index3, yn, cbn, crn);
            index1 = num4 + 1;
        }

        int num = flag ? 1 : 0;
        return bluRaySupPalette;
    }

    public class PcsData
    {
        public int CompNum { get; init; }

        public CompositionState CompositionState { get; init; }

        public bool PaletteUpdate { get; init; }

        public long StartTime { get; set; }

        public long EndTime { get; set; }

        public Size Size { get; init; }

        public int FramesPerSecondType { get; set; }

        public int PaletteId { get; init; }

        public List<PcsObject> PcsObjects { get; set; }

        public string Message { get; set; }

        public List<List<OdsData>> BitmapObjects { get; set; }

        public List<PaletteInfo> PaletteInfos { get; set; }

        public bool IsForced
        {
            get { return PcsObjects.Any((Func<PcsObject, bool>) (obj => obj.IsForced)); }
        }

        public Position GetPosition()
        {
            return PcsObjects.Count > 0 ? new Position(PcsObjects.Min((Func<PcsObject, int>) (p => p.Origin.X)), this.PcsObjects.Min<PcsObject>((Func<PcsObject, int>) (p => p.Origin.Y))) : new Position(0, 0);
        }

        public TimeCode StartTimeCode => new TimeCode(StartTime / 90.0);

        public TimeCode EndTimeCode => new TimeCode(EndTime / 90.0);
    }

    private class PdsData
    {
        public string Message { get; set; }

        public int PaletteId { get; set; }

        public int PaletteVersion { get; set; }

        public PaletteInfo PaletteInfo { get; set; }
    }

    public class OdsData
    {
        public int ObjectId { get; set; }

        public int ObjectVersion { get; set; }

        public string Message { get; set; }

        public bool IsFirst { get; init; }

        public Size Size { get; init; }

        public ImageObjectFragment Fragment { get; init; }
    }

    public enum CompositionState
    {
        Normal,
        AcquPoint,
        EpochStart,
        EpochContinue,
        Invalid,
    }

    private class DeleteIndex
    {
        public int Number { get; init; }

        public int Index { get; init; }
    }

    public class PaletteInfo
    {
        public int PaletteSize { get; init; }
        public byte[] PaletteBuffer { get; set; }
    }
}
