using System;
using System.IO;
using System.Text;

namespace net.ayutet.get.meta
{
    /// <summary>
    /// GetMetaDataFlac
    /// 使い方はNewする際にに引数に楽曲ファイル(.flac)を指定する。
    /// 同フォルダに.jpgのアルバムアートが出力されるよ。
    ///    var ****** = new GetMetaDataFlac(ファイルパス);
    /// あとは楽曲データが格納されます。
    /// .Music_Album_Title      アルバムタイトル
    /// .Music_Album_Artist     アルバムアーティスト名
    /// .Music_Title            曲タイトル
    /// .Music_Artist           曲アーティスト
    /// </summary>
    class GetMetaDataFlac
    {
        // FLACのメタデータについての仕様はこちら
        // https://xiph.org/flac/format.html
        //
        private const string HEAD_Marker = "fLaC";     // マーカー "fLaC"

        private const int HEAD_MarkerSize = 4;         // マーカーサイズ
        private const int META_BLOCK = 4;              // メタデータブロック 下記フラグ(1)+サイズ(3)
        private const int META_Flag = 1;               // メタデータフラグ 1bit+7bit
        private const int META_ByteSize = 3;           // メタデータサイズ
        private const int META_Comment_ByteSize = 4;   // メタデータコメントサイズ
        private const int META_Comment_Count = 4;      // メタデータコメント個数
        private const int META_Comment_Wordsize = 4;   // コメントサイズ

        private const int META_Pict_Type = 4;          // 画像タイプ
        private const int META_Pict_MimeSize = 4;      // 画像MIMEサイズ
        private const int META_Pict_InfoSize = 4;      // 画像説明サイズ
        private const int META_Pict_Width = 4;         // 画像横サイズ　(空データ)
        private const int META_Pict_Height = 4;        // 画像縦サイズ　(空データ)
        private const int META_Pict_ColorDips = 4;     // 画像色深度　(空データ)
        private const int META_Pict_Color = 4;         // 画像色数　(空データ)
        private const int META_Pict_byteSize = 4;      // 画像データサイズ

        // 楽曲情報
        string Music_Album_Title;                // アルバムタイトル
        string Music_Album_Artist;               // アルバムアーティスト名
        string Music_Title;                      // 曲タイトル
        string Music_Artist;                     // 曲アーティスト
        string Mime_Type;                        // アルバムアートのMIMEタイプ

        // アルバムジャケットデータ
        byte[] AlbumArt;

        /// <summary>
        /// コンストラクタ(引数1)
        /// </summary>
        /// <param name="file_path">ファイルパス</param>
        /// <returns></returns>
        /// 
        public GetMetaDataFlac(string file_path)
        {
            ////ファイルを読み込む 後で動的に変える
            //if (string.IsNullOrEmpty(file_path))
            //{
            //    file_path = @"C:\Temp\Sample_BeeMoved_96kHz24bit.flac";
            //}
            ////ファイルを読み込む 後で動的に変える

            //ファイル読込み用[stream]作成
            using (Stream stream = File.OpenRead(file_path))
            {
                // [stream]からバイナリ単位で読込み用[BinaryReader]作成
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // ヘッダ"fLaC"を読み込む
                    if (Encoding.ASCII.GetString(reader.ReadBytes(HEAD_MarkerSize)) != HEAD_Marker)
                    {
                        Console.WriteLine("FLACファイルじゃありません。");
                        return;
                    }

                    GetFlacMetaData(reader);

                    // アルバム画像が作成できていれば画像出力
                    if (AlbumArt.Length != 0) { CreateAlbumArtData(Path.GetDirectoryName(file_path) + "\\" + Path.GetFileNameWithoutExtension(file_path) + Mime_Type); }
                    return;
                }
            }

        }

        public GetMetaDataFlac(string file_path, string out_file_path)
        {
            //ファイル読込み用[stream]作成
            using (Stream stream = File.OpenRead(file_path))
            {
                // [stream]からバイナリ単位で読込み用[BinaryReader]作成
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // ヘッダ"fLaC"を読み込む
                    if (Encoding.ASCII.GetString(reader.ReadBytes(HEAD_MarkerSize)) != HEAD_Marker)
                    {
                        Console.WriteLine("FLACファイルじゃありません。");
                        return;
                    }

                    GetFlacMetaData(reader);

                    // アルバム画像が作成できていれば画像出力
                    if (AlbumArt.Length != 0) { CreateAlbumArtData(Path.GetDirectoryName(out_file_path) + "\\" + Path.GetFileNameWithoutExtension(file_path) + Mime_Type); }
                    return;
                }
            }

        }

        /// <summary>
        /// FLAC用METAデータ取得
        /// </summary>
        /// <param name="InData"></param>
        /// <returns>Boolean</returns>
        private bool GetFlacMetaData(BinaryReader InData)
        {
            // FLAC Header Information
            // Marker:4byte "fLaC" 固定
            //  [METADATA]
            //   LastDataFlag 1bit 0以外だと最後のメタデータ
            //   MetaDataBloc 7bit
            //     0 : STREAMINFO
            //     1 : PADDING
            //     2 : APPLICATION
            //     3 : SEEKTABLE
            //     4 : VORBIS_COMMENT ｢重要」曲タイトル情報
            //     5 : CUESHEET
            //     6 : PICTURE 「重要」アルバムワーク

            string METAFLAG;
            do
            {
                //METAデータブロック1Byte取得(1bit+7bit)
                METAFLAG = BitConverter.ToString(InData.ReadBytes(META_Flag));

                switch (METAFLAG.Substring(1, 1))
                {
                    // "4" VORBIS_COMMENT 曲名とか
                    case "4":
                        GetFlacCommentData(InData);
                        break;

                    // "6" PICTURE アルバムアート
                    case "6":
                        GetFlacAlbumArtData(InData);
                        break;

                    default:
                        //関係ないヘッダなので次3バイト(META_ByteSize)で指定されたサイズ分読み捨てる
                        InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(InData.ReadBytes(META_ByteSize)).Replace("-", ""), 16)); //ベンダコメントサイズ読み捨て
                        break;
                }
                // メタブロック先頭が1だった場合はMETAデータ終了
            } while (METAFLAG.Substring(0, 1) == "0");
            return true;
        }

        /// <summary>
        /// FLAC用コメントデータ取得
        /// </summary>
        /// <param name="Indata"></param>
        /// <returns>Boolean</returns>
        private bool GetFlacCommentData(BinaryReader InData)
        {
            //     4 : VORBIS_COMMENTのメタ内容
            //         ベンダコメントサイズ:4byte
            //         ベンダコメント:上記4byteで指定された全体サイズ
            //           コメントの個数:4byte (この個数分繰り返し)
            //           コメントサイズ:4byte
            //           コメント:上記4byteで指定されたサイズ
            //            META_Comment_ByteSize

            byte[] LE_DataSize;
            string CommentData, CommentDataUpper;
            int LoopCnt;

            // コメント全体サイズは読み捨てる
            InData.ReadBytes(META_ByteSize);

            // この4バイトがリトルエンディアンでサイズ持ってるので反転して読み捨て
            LE_DataSize = InData.ReadBytes(META_Comment_ByteSize);
            Array.Reverse(LE_DataSize);
            InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(LE_DataSize).Replace("-", ""), 16)); // 読み捨て

            // コメントの個数4バイト(リトルエンディアン)
            LE_DataSize = InData.ReadBytes(META_Comment_Count);
            Array.Reverse(LE_DataSize);
            LoopCnt = Convert.ToInt32(BitConverter.ToString(LE_DataSize).Replace("-", ""), 16);

            // コメントの個数分ループする。
            for (int i = 0; i < LoopCnt; i++)
            {
                // 次のコメントデータサイズ4バイト(リトルエンディアン)
                LE_DataSize = InData.ReadBytes(META_Comment_ByteSize);
                Array.Reverse(LE_DataSize);
                CommentData = System.Text.Encoding.UTF8.GetString(InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(LE_DataSize).Replace("-", ""), 16)));
                CommentDataUpper = CommentData.ToUpper();

                // 欲しい情報が格納されているか判断してデータを格納
                if (CommentDataUpper.IndexOf("ALBUM=") == 0) { Music_Album_Title = CommentData.Substring(6); }
                if (CommentDataUpper.IndexOf("TITLE=") == 0) { Music_Title = CommentData.Substring(6); }
                if (CommentDataUpper.IndexOf("ARTIST=") == 0) { Music_Artist = CommentData.Substring(7); }
                if (CommentDataUpper.IndexOf("ALBUMARTIST=") == 0) { Music_Album_Artist = CommentData.Substring(12); }

            }
            return true;
        }

        /// <summary>
        /// FLAC用アルバムデータ取得
        /// </summary>
        /// <param name="Indata"></param>
        /// <returns>Boolean</returns>
        private bool GetFlacAlbumArtData(BinaryReader InData)
        {
            //     6 : PICTUREのメタ内容
            //     　　画像タイプ:4byte
            //         MIMEタイプサイズ:4byte
            //         MIMEタイプ:上記4byteで指定されたサイズ
            //         画像説明サイズ:4byte
            //         画像説明:上記4byteで指定されたサイズ
            //         画像幅:4byte
            //         画像高:4byte
            //         色深度:4byte
            //         色数:4byte
            //         画像サイズ:4byte
            //         画像データ:上記4byteで指定されたサイズ(これをファイルに書き込むと画像データになるよ)

            // コメント全体サイズは読み捨てる
            InData.ReadBytes(META_ByteSize);

            // 画像タイプは読み捨てる 03:フロント もしかして・・・03以外は捨てたほうがいいのかしら？
            InData.ReadBytes(META_Pict_Type);

            // MIMEタイプ
            //InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(InData.ReadBytes(META_Pict_MimeSize)).Replace("-", ""), 16));
            Mime_Type = System.Text.Encoding.UTF8.GetString(InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(InData.ReadBytes(META_Pict_MimeSize)).Replace("-", ""), 16)));
            //取得したMIMEから画像拡張子決定
            SetPictureType(Mime_Type.ToUpper());

            // 画像説明読み捨て
            InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(InData.ReadBytes(META_Pict_InfoSize)).Replace("-", ""), 16));

            // 画像幅桁色深度情報は無いので読み捨て
            InData.ReadBytes(META_Pict_Width);
            InData.ReadBytes(META_Pict_Height);
            InData.ReadBytes(META_Pict_ColorDips);
            InData.ReadBytes(META_Pict_byteSize);

            // 画像サイズ(4byte)分のデータを読み込み
            AlbumArt = InData.ReadBytes(Convert.ToInt32(BitConverter.ToString(InData.ReadBytes(META_Pict_byteSize)).Replace("-", ""), 16));

            return true;
        }

        /// <summary>
        /// 画像データ出力
        /// </summary>
        /// <param name="OutAlbumArtPath"></param>
        /// <returns>Boolean</returns>
        private bool CreateAlbumArtData(string OutAlbumArtPath)
        {
            //画像データ出力
            using (Stream streamwriter = File.OpenWrite(OutAlbumArtPath))
            {
                // streamに書き込むためのBinaryWriterを作成
                using (BinaryWriter writer = new BinaryWriter(streamwriter))
                {
                    // intの数値を書き込む
                    writer.Write(AlbumArt);
                }
            }
            return true;
        }

        private bool SetPictureType(string in_mime_upper)
        {
            // MIME TYPEより画像の拡張子を判別し、出力ファイル名を作成する MIME_TYPEは拡張子に上書き
            Mime_Type = string.Empty;
            if (in_mime_upper.IndexOf("JPEG") >= 0) { Mime_Type = ".jpg"; }

            return true;
        }
    }
}
