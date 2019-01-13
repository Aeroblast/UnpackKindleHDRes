using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.InteropServices;


//简单重现python脚本过程 https://www.mobileread.com/forums/showpost.php?p=3114050&postcount=1145
//改进文件命名
//【可选】读取KindleUnpack的assembled_text.dat和xhtml，替换图片
namespace UnpackKindleHDRes
{
    struct SectionInfo
    {
        public ulong start_addr;
        public ulong end_addr;
        public ulong length { get { return end_addr - start_addr; } }
    }

    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct HeaderInfo
    {
        public UInt32 title_length;
        public UInt32 title_offset;
        public UInt32 unknown2;
        public UInt32 offset_to_hrefs;
        public UInt32 num_wo_placeholders;

        public UInt32 num_resc_recs;
        public UInt32 unknown1;
        public UInt32 unknown0;
        public UInt32 codepage;
        public UInt16 count;
        public UInt16 type;
        public UInt32 record_size;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] magic;

    }
    class Program
    {
        static byte[] azw6_data;
        static ushort section_count = 0;
        static SectionInfo[] section_info;
        public static string title;
        static string[] hrefs;
        static string save_path = "OutputImage";
        static string[] saved_paths;
        static int saved_count = 0;
        static bool epub=false;
        static void Main(string[] args)
        {
            if (args.Length == 0 || !File.Exists(args[0])) { Console.WriteLine("Give me .azw.res file plz."); return; }
            if (args.Length >= 2) save_path = args[1];
            if(Path.GetFileName(save_path)=="assembled_text.dat")
            {
                if(!File.Exists(save_path)){Console.WriteLine("Plz Check the KindleUnpack output.");return;}
                save_path=Path.GetDirectoryName(save_path);
                if(!Directory.Exists(save_path)){Console.WriteLine("Plz Check the Output path.");return;}
                epub=true;
            }else
            if (!Directory.Exists(save_path)) 
            {
                if(File.Exists(save_path)){Console.WriteLine("Plz Check the Output path.");return;}
                 Directory.CreateDirectory(save_path); 
            }
            Console.OutputEncoding = Encoding.UTF8;
            azw6_data = File.ReadAllBytes(args[0]);
            string r;
            Console.WriteLine("=========Dump Sections:");
            r = DumpSections(); Console.WriteLine(r); if (r != "OK") { return; }

            Console.WriteLine("=========Header Section Info:");
            r = ReadHeaderSection(); Console.WriteLine(r); if (r != "OK") { return; }

            Console.WriteLine("=========Read Sections:");
            if (saved_paths == null) { Console.WriteLine("Error"); return; }
            foreach (SectionInfo info in section_info)
            {
                r = ReadSection(GetSectionData(info)); if (r != "OK") { Console.WriteLine(r); return; }
            }
            hrefs=VaildHref(hrefs);
            if(hrefs.Length==(int)saved_paths.Length)
            {
                int i=0,j=0;
                Regex regex=new Regex("kindle:embed:(.*?)\\?mime=image/(.*)");
                for(;i<hrefs.Length&&j<saved_paths.Length;)
                {

                    while(!regex.Match(hrefs[i]).Success){i++;}
                    Match match=regex.Match(hrefs[i]);
                    string target_path=Path.Combine(save_path,match.Groups[1].Value+"."+match.Groups[2].Value);
                    if(File.Exists(target_path))File.Delete(target_path);
                    File.Move(
                        saved_paths[j],
                        target_path
                    );
                    saved_paths[i]=target_path;
                    j++;i++;

                }

            }
            else
            {
                Console.WriteLine("Rename Failure");return;
            }

            if(epub)
            {
                Console.WriteLine("=======Rename to KindleUnpack Epub");
               r= ReplaceEpubImage();
               Console.WriteLine(r);
            }
            Console.WriteLine("END");

        }
        static string DumpSections()
        {
            string ident = Encoding.ASCII.GetString(azw6_data, 0x3c, 8);
            if (ident != "RBINCONT") return "ident failure";//保证是一个HD容器
            section_count = GetUInt16(azw6_data, 76);
            section_info = new SectionInfo[section_count];

            section_info[0].start_addr = GetUInt32(azw6_data, 78);
            for (uint i = 1; i < section_count; i++)
            {

                section_info[i].start_addr = GetUInt32(azw6_data, 78 + i * 8);//中间有个字段是0 2 4 6 8。。。不知道啥意思
                section_info[i - 1].end_addr = section_info[i].start_addr;
            }
            section_info[section_count - 1].end_addr = (ulong)azw6_data.Length;

            return "OK";
        }


        static Dictionary<uint, string> id_map_strings = new Dictionary<uint, string>
        { 
            //copy from python code, ([0-9]{1,3}) : '(.*?)' ----> {\1,"\2"} then a few fixes
           {1,"Drm Server Id (1)"},
           {2,"Drm Commerce Id (2)"},
           {3,"Drm Ebookbase Book Id(3)"},
           {100,"Creator_(100)"},
           {101,"Publisher_(101)"},
           {102,"Imprint_(102)"},
           {103,"Description_(103)"},
           {104,"ISBN_(104)"},
           {105,"Subject_(105)"},
           {106,"Published_(106)"},
           {107,"Review_(107)"},
           {108,"Contributor_(108)"},
           {109,"Rights_(109)"},
           {110,"SubjectCode_(110)"},
           {111,"Type_(111)"},
           {112,"Source_(112)"},
           {113,"ASIN_(113)"},
           {114,"versionNumber_(114)"},
           {117,"Adult_(117)"},
           {118,"Price_(118)"},
           {119,"Currency_(119)"},
           {122,"fixed-layout_(122)"},
           {123,"book-type_(123)"},
           {124,"orientation-lock_(124)"},
           {126,"original-resolution_(126)"},
           {127,"zero-gutter_(127)"},
           {128,"zero-margin_(128)"},
           {129,"K8_Masthead/Cover_Image_(129)"},
           {132,"RegionMagnification_(132)"},
           {200,"DictShortName_(200)"},
           {208,"Watermark_(208)"},
           {501,"cdeType_(501)"},
           {502,"last_update_time_(502)"},
           {503,"Updated_Title_(503)"},
           {504,"ASIN_(504)"},
           {508,"Unknown_Title_Furigana?_(508)"},
           {517,"Unknown_Creator_Furigana?_(517)"},
           {522,"Unknown_Publisher_Furigana?_(522)"},
           {524,"Language_(524)"},
           {525,"primary-writing-mode_(525)"},
           {526,"Unknown_(526)"},
           {527,"page-progression-direction_(527)"},
           {528,"override-kindle_fonts_(528)"},
           {529,"Unknown_(529)"},
           {534,"Input_Source_Type_(534)"},
           {535,"Kindlegen_BuildRev_Number_(535)"},
           {536,"Container_Info_(536)"}, // CONT_Header is 0, Ends with CONTAINER_BOUNDARY (or Asset_Type?)
           {538,"Container_Resolution_(538)"},
           {539,"Container_Mimetype_(539)"},
           {542,"Unknown_but_changes_with_filename_only_(542)"},
           {543,"Container_id_(543)"},  // FONT_CONTAINER, BW_CONTAINER, HD_CONTAINER
           {544,"Unknown_(544)"}
        };

        static Dictionary<uint, string> id_map_values = new Dictionary<uint, string>()
        {
        {115,"sample_(115)"},
           {116,"StartOffset_(116)"},
           {121,"K8(121)_Boundary_Section_(121)"},
           {125,"K8_Count_of_Resources_Fonts_Images_(125)"},
           {131,"K8_Unidentified_Count_(131)"},
           {201,"CoverOffset_(201)"},
           {202,"ThumbOffset_(202)"},
           {203,"Fake_Cover_(203)"},
           {204,"Creator_Software_(204)"},
           {205,"Creator_Major_Version_(205)"},
           {206,"Creator_Minor_Version_(206)"},
           {207,"Creator_Build_Number_(207)"},
           {401,"Clipping_Limit_(401)"},
           {402,"Publisher_Limit_(402)"},
           {404,"Text_to_Speech_Disabled_(404)"}
        };

        static Dictionary<uint, string> id_map_hex = new Dictionary<uint, string>()
        {
            { 209 , "Tamper_Proof_Keys_(209_in_hex)"},
           {300 , "Font_Signature_(300_in_hex)"}
        };

        static string ReadHeaderSection()
        {
            int header_size = Marshal.SizeOf(typeof(HeaderInfo));
            Byte[] header_raw = SubArray(azw6_data, section_info[0].start_addr, (ulong)header_size);
            Array.Reverse(header_raw);

            IntPtr structPtr = Marshal.AllocHGlobal(header_size);
            Marshal.Copy(header_raw, 0, structPtr, header_size);
            HeaderInfo header0 = (HeaderInfo)Marshal.PtrToStructure(structPtr, typeof(HeaderInfo));
            Marshal.FreeHGlobal(structPtr);
            Array.Reverse(header0.magic);
            if (header0.codepage != 65001) return "Not UTF8!?";
            Byte[] title_raw = SubArray(azw6_data, section_info[0].start_addr + header0.title_offset, header0.title_length);
            title = Encoding.UTF8.GetString(title_raw);
            Console.WriteLine(" Title:" + title);
            Byte[] ext = SubArray(azw6_data,
            section_info[0].start_addr + 48,
            section_info[0].length - 48
            );
            UInt32 len = GetUInt32(ext, 4);
            UInt32 num_items = GetUInt32(ext, 8);
            uint pos = 12;
            for (int i = 0; i < num_items; i++)
            {
                UInt32 id = GetUInt32(ext, pos);
                UInt32 size = GetUInt32(ext, pos + 4);
                if (id_map_strings.ContainsKey(id))
                {
                    string a = Encoding.UTF8.GetString(SubArray(ext, pos + 8, size - 8));
                    Console.WriteLine(" " + id_map_strings[id] + ":" + a);

                }
                else
                if (id_map_values.ContainsKey(id))
                {
                    UInt32 a = 0;
                    switch (size)
                    {
                        case 9: a = GetUInt8(ext, pos + 8); break;
                        case 10: a = GetUInt16(ext, pos + 8); break;
                        case 12: a = GetUInt32(ext, pos + 8); break;
                        default: Console.Write("unexpected size!"); break;
                    }
                    if (id == 125) { saved_paths = new string[a]; }//count of res
                    Console.WriteLine(" " + id_map_values[id] + ":" + a);
                }
                else
                if (id_map_hex.ContainsKey(id))
                {
                    string a = ToHexString(ext, pos + 8, size - 8);
                    Console.WriteLine(" " + id_map_hex[id] + ":" + a);
                }
                else
                {
                    string a = ToHexString(ext, pos + 8, size - 8);
                    Console.WriteLine(" unknown id " + id + ":" + a);
                }

                pos += size;
            }
            return "OK";
        }

        static byte[] GetSectionData(SectionInfo info)
        {
            Byte[] d = SubArray(azw6_data, info.start_addr, info.length);
            return d;
        }

        static Dictionary<UInt32, string> sec_type_map = new Dictionary<UInt32, string>()
        {
            {i32("FONT"),"FONT"},
            {i32("RESC"),"RESC"},
            {i32("CRES"),"CRES"},
            {i32("CONT"),"CONT: Header"},
            {0xa0a0a0a0,"Empty Image"},
            {0xe98e0000+('\r'<<8)+'\n',"EOF_RECORD"},
            {i32("kind"),"KINDLE:EMBED"},
        };

        static string ReadSection(byte[] section_data)
        {
            UInt32 x = i32(section_data);
            if (sec_type_map.ContainsKey(x))
            {
                Console.Write(" Type:" + sec_type_map[x]);
                if (x == i32("kind"))
                {
                    hrefs = Encoding.UTF8.GetString(section_data).Split('|');
                }
                else
                if (x == i32("CRES"))
                {
                    Console.Write(
                    ReadCRES(SubArray(section_data, 12, (ulong)section_data.Length - 12))
                    );
                }
                Console.WriteLine();
            }
            return "OK";
        }
        static int count = 0;
        static string ReadCRES(byte[] data)
        {
            if (data[0] == 0xff && data[1] == 0xd8)
            {
                count++;
                string path = Path.Combine(save_path, count + ".jpg");
                saved_paths[saved_count] = path; saved_count++;
                File.WriteAllBytes(path, data);
                return "           Saved " + path;
            }
            return "Unhandled Format";
        }
        static string ReplaceEpubImage()
        {
            string raw_text=File.ReadAllText(Path.Combine(save_path,"assembled_text.dat"));
            Regex regex=new Regex("(<html xmlns[\\s\\S]*?</html>)");
            MatchCollection ms= regex.Matches(raw_text);
            List<string> raws=new List<string>();
            foreach(Match m in ms)raws.Add(m.Groups[1].Value);
            string [] infos=Directory.GetFiles(Path.Combine(save_path,"OEBPS\\Text"));
            List<string> texts=new List<string>();
            regex=new Regex("part[0-9]{4}\\.xhtml");
            foreach(string s in infos)
            {
                if(regex.Match(Path.GetFileName(s)).Success){texts.Add(s);}
            }
            if(raws.Count!=texts.Count)
            {
                return "Epub Proc Failure.";
            }
            for(int i=0;i<saved_paths.Length;i++)
            {
                bool found=false;
                for(int j=0;j<raws.Count;j++)
                {
                    if(raws[j].Contains(hrefs[i]))
                    {
                        string[]raw_lines=raws[j].Split('\n');
                        string[]epub_lines=File.ReadAllLines(texts[j]);
                        if(raw_lines.Length!=epub_lines.Length){return "text error";}
                        regex=new Regex("src=\"(.*)\"");
                        for(int k=0;k<raw_lines.Length;k++)
                        {
                            MatchCollection raw_matches=regex.Matches(raw_lines[k]);
                            if(raw_matches.Count==0)continue;
                            MatchCollection epub_matches=regex.Matches(epub_lines[k]);
                            if(raw_matches.Count!=epub_matches.Count){return "match error";}
                            for(int n=0;n<raw_matches.Count;n++)
                            {
                                if(raw_matches[n].Groups[1].Value==hrefs[i])
                                {
                                    found=true;
                                    string new_name=Path.Combine(
                                        save_path,
                                    Path.GetFileName(epub_matches[n].Groups[1].Value) 
                                    );
                                    File.Move(saved_paths[i],new_name);
                                    Console.WriteLine(" Rename "+Path.GetFileName(saved_paths[i])+" to "+Path.GetFileName(epub_matches[n].Groups[1].Value));
                                    break;
                                }
                            }
                            if(found)break;
                        }//search in every line
                        

                    }
                    if(found)break;
                    
                }//search in every xhtml 

            }//for each href
            return "OK";
        }
        static string[] VaildHref(string[] hs)
        {
            List<string> ist=new List<string>();
            Regex regex=new Regex("kindle:embed:(.*?)\\?mime=image/(.*)");
            foreach (string s in hs) { if (regex.Match(s).Success) ist.Add(s); }
            return ist.ToArray();
        }
        //utils
        static byte[] SubArray(byte[] src, ulong start, ulong length)
        {
            byte[] r = new byte[length];
            for (ulong i = 0; i < length; i++) { r[i] = src[start + i]; }
            return r;
        }
        static string ToHexString(byte[] src, uint start, uint length)
        {
            //https://stackoverflow.com/a/14333437/48700
            char[] c = new char[length * 2];
            int b;
            for (int i = 0; i < length; i++)
            {
                b = src[start + i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = src[start + i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }

        static UInt64 GetUInt64(byte[] src, ulong start)
        {
            byte[] t = SubArray(src, start, 8);
            Array.Reverse(t);
            return BitConverter.ToUInt64(t);
        }
        static UInt32 GetUInt32(byte[] src, ulong start)
        {
            byte[] t = SubArray(src, start, 4);
            Array.Reverse(t);
            return BitConverter.ToUInt32(t);
        }
        static UInt16 GetUInt16(byte[] src, ulong start)
        {
            byte[] t = SubArray(src, start, 2);
            Array.Reverse(t);
            return BitConverter.ToUInt16(t);
        }
        static byte GetUInt8(byte[] src, ulong start)
        {
            return src[start];
        }
        static UInt32 i32(string a)
        {
            return
            (((uint)a[0]) << 24)
            + (((uint)a[1]) << 16)
            + (((uint)a[2]) << 8)
            + (((uint)a[3]));
        }
        static UInt32 i32(byte[] a)
        {
            return
            (((uint)a[0]) << 24)
            + (((uint)a[1]) << 16)
            + (((uint)a[2]) << 8)
            + (((uint)a[3]));
        }
    }
}
