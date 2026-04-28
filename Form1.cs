using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace ZXAnimPacker128
{
    public partial class Form1 : Form
    {
        const int ScreenSize = 6912;
        const int ScreenBase = 16384;
        const byte CommandRawRun = 0xDB;
        const byte CommandCountedLoop = 0xDC;
        const byte CommandEndlessLoop = 0xDD;
        const byte CommandEndFrame = 0xDE;
        const byte CommandRleKeyFrame = 0xDF;
        const byte CommandSetBorder = 0xF0;
        const byte CommandRawKeyFrame = 0xFF;
        static readonly Color[] SpectrumColors = new Color[]
        {
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(0, 0, 205),
            Color.FromArgb(205, 0, 0),
            Color.FromArgb(205, 0, 205),
            Color.FromArgb(0, 205, 0),
            Color.FromArgb(0, 205, 205),
            Color.FromArgb(205, 205, 0),
            Color.FromArgb(205, 205, 205),
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(0, 0, 255),
            Color.FromArgb(255, 0, 0),
            Color.FromArgb(255, 0, 255),
            Color.FromArgb(0, 255, 0),
            Color.FromArgb(0, 255, 255),
            Color.FromArgb(255, 255, 0),
            Color.FromArgb(255, 255, 255)
        };

        public Form1()
        {
            InitializeComponent();
            listBox1.SelectedIndexChanged += new EventHandler(listBox1_SelectedIndexChanged);
            pictureBox1.Click += new EventHandler(pictureBox1_Click);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            pickfiles();
        }

        private void Pack()
        {
            byte delay = Convert.ToByte(textBox1.Text);
            int idx = 0;
            byte[] PackArray = new byte[6912];
            int offset = 6912;
            do
            {
                int first = idx;
                int second = idx + 1;
                if (second > listBox1.Items.Count - 1)
                {
                    //reload last frame and compare with first frame to generate a loop.
                    second = 0;
                }
                //listBox2.Items.Add(first.ToString() + " vs. " + second.ToString());
                byte[] DeltabyteArray = ReadBytesFromFile(listBox1.Items[first].ToString(), 6912);
                byte[] ComparebyteArray = ReadBytesFromFile(listBox1.Items[second].ToString(), 6912);
                
                if (idx == 0)
                {
                    Array.Copy(DeltabyteArray, PackArray, 6912);
                    listBox2.Items.Add("1.Frame, NewBytes:" + (PackArray.Length).ToString());
                    
                }
                List<Difference> differences = FindDifferences(DeltabyteArray, ComparebyteArray);

                int lastidx = 0;
                for (int x = 0; x < differences.Count; x++)
                {
                    if ((lastidx<6144) && (differences[x].Index>=6144))
                    {
                        lastidx=6500; //no more skip
                        PackArray = AddBytes(PackArray, 255, 254, 0); //MARK as attribute section
                    }
                    byte[] bytes = BitConverter.GetBytes(differences[x].Index+16384);
                    PackArray = AddBytes(PackArray, bytes[1], bytes[0], differences[x].Value2);
                    //listBox2.Items.Add((differences[x].Index + 16384).ToString() + ":" + bytes[1].ToString() + "-" + bytes[0].ToString() + "=" + differences[x].Value2.ToString());
                }
                PackArray = AddBytes(PackArray, 255, 255, delay); //mark end of frame
                listBox2.Items.Add((1 + idx).ToString() + ".Frame, NewBytes:" + (PackArray.Length - offset).ToString());
                offset = PackArray.Length;
                idx++;
            } while (idx<listBox1.Items.Count);
            PackArray = AddBytes(PackArray, 255, 253, 0); //end of frames.

            listBox2.Items.Add("Total Bytes:" + PackArray.Length.ToString());
            if (PackArray.Length < 32768) listBox2.Items.Add("Great! it fits in two banks! Still 48K!");
            string fileName = Path.GetFileNameWithoutExtension(listBox1.Items[0].ToString());
            File.WriteAllBytes(fileName+".zxa", PackArray);
            listBox2.Items.Add("File Saved:" + fileName + ".zxa");
        }

        private void PackV2()
        {
            byte delay = Convert.ToByte(textBox1.Text);
            byte keyFrameDelay = delay > 2 ? (byte)(delay - 2) : (byte)0;
            byte loopCount = Convert.ToByte(txtLoopCount.Text);
            int borderIndex = ComboBorder.SelectedIndex;
            if (borderIndex < 0) borderIndex = 0;
            if (borderIndex > 7) borderIndex = 7;

            if (listBox1.Items.Count == 0)
            {
                listBox2.Items.Add("No files selected.");
                return;
            }

            List<byte[]> frames = new List<byte[]>();
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                frames.Add(ReadBytesFromFile(listBox1.Items[i].ToString(), ScreenSize));
            }

            bool compressFirstFrame = chkCompressFirstFrame.Checked;
            List<byte> packBytes = new List<byte>(ScreenSize + (frames.Count * 256));
            packBytes.Add((byte)(CommandSetBorder + borderIndex));
            listBox2.Items.Add("V2 Border:" + borderIndex.ToString());
            AddKeyFrameV2(packBytes, frames[0], compressFirstFrame);
            packBytes.Add(CommandEndFrame);
            packBytes.Add(keyFrameDelay);
            listBox2.Items.Add("V2 1.Frame, KeyBytes:" + (packBytes.Count - 1).ToString());

            int offset = packBytes.Count;
            for (int idx = 0; idx < frames.Count - 1; idx++)
            {
                List<byte> deltaBytes = new List<byte>();
                List<Difference> differences = FindDifferences(frames[idx], frames[idx + 1]);
                EncodeDifferencesV2(deltaBytes, differences);
                List<byte> keyFrameBytes = CreateKeyFrameV2(frames[idx + 1], compressFirstFrame);

                if (keyFrameBytes.Count < deltaBytes.Count)
                {
                    packBytes.AddRange(keyFrameBytes);
                    packBytes.Add(CommandEndFrame);
                    packBytes.Add(keyFrameDelay);
                    listBox2.Items.Add("V2 " + (2 + idx).ToString() + ".Frame, KeyFrameBytes:" + keyFrameBytes.Count.ToString());
                }
                else
                {
                    packBytes.AddRange(deltaBytes);
                    packBytes.Add(CommandEndFrame);
                    packBytes.Add(delay);
                }

                listBox2.Items.Add("V2 " + (2 + idx).ToString() + ".Frame, NewBytes:" + (packBytes.Count - offset).ToString());
                offset = packBytes.Count;
            }

            if (loopCount == 0)
            {
                packBytes.Add(CommandEndlessLoop);
                listBox2.Items.Add("V2 Loop: Endless");
            }
            else
            {
                packBytes.Add(CommandCountedLoop);
                packBytes.Add(loopCount);
                listBox2.Items.Add("V2 Loop Count:" + loopCount.ToString());
            }

            byte[] packArray = packBytes.ToArray();
            listBox2.Items.Add("V2 Total Bytes:" + packArray.Length.ToString());
            if (packArray.Length < 32768) listBox2.Items.Add("Great! it fits in two banks! Still 48K!");
            string fileName = Path.GetFileNameWithoutExtension(listBox1.Items[0].ToString());
            File.WriteAllBytes(fileName + ".zxa", packArray);
            listBox2.Items.Add("File Saved:" + fileName + ".zxa");
        }

        private static List<byte> CreateKeyFrameV2(byte[] frame, bool compress)
        {
            List<byte> output = new List<byte>(frame.Length + 1);
            AddKeyFrameV2(output, frame, compress);
            return output;
        }

        private static void AddKeyFrameV2(List<byte> output, byte[] frame, bool compress)
        {
            if (compress)
            {
                AddRleKeyFrameV2(output, frame);
            }
            else
            {
                AddRawKeyFrameV2(output, frame);
            }
        }

        private static void AddRawKeyFrameV2(List<byte> output, byte[] frame)
        {
            output.Add(CommandRawKeyFrame);
            output.AddRange(frame);
        }

        private static void AddRleKeyFrameV2(List<byte> output, byte[] frame)
        {
            output.Add(CommandRleKeyFrame);

            int index = 0;
            while (index < frame.Length)
            {
                int repeatLength = GetRepeatLength(frame, index, 128);
                if (repeatLength >= 3)
                {
                    output.Add((byte)(0x80 + repeatLength - 1));
                    output.Add(frame[index]);
                    index += repeatLength;
                    continue;
                }

                int literalStart = index;
                int literalLength = 0;
                while ((index < frame.Length) && (literalLength < 128))
                {
                    repeatLength = GetRepeatLength(frame, index, 128);
                    if ((repeatLength >= 3) && (literalLength > 0))
                    {
                        break;
                    }

                    if (repeatLength >= 3)
                    {
                        break;
                    }

                    index++;
                    literalLength++;
                }

                output.Add((byte)(literalLength - 1));
                for (int i = 0; i < literalLength; i++)
                {
                    output.Add(frame[literalStart + i]);
                }
            }
        }

        private static int GetRepeatLength(byte[] bytes, int start, int maxLength)
        {
            int length = 1;
            while ((start + length < bytes.Length) &&
                   (length < maxLength) &&
                   (bytes[start + length] == bytes[start]))
            {
                length++;
            }

            return length;
        }

        private static void EncodeDifferencesV2(List<byte> output, List<Difference> differences)
        {
            bool[] used = new bool[differences.Count];

            for (int i = 0; i < differences.Count; )
            {
                int runLength = 1;
                while ((i + runLength < differences.Count) &&
                       (differences[i + runLength].Index == differences[i + runLength - 1].Index + 1))
                {
                    runLength++;
                }

                if (runLength >= 3)
                {
                    AddRawRunsV2(output, differences, used, i, runLength);
                }

                i += runLength;
            }

            List<Difference>[] pageBuckets = new List<Difference>[0x5B];
            for (int i = 0; i < differences.Count; i++)
            {
                if (used[i]) continue;

                int address = differences[i].Index + ScreenBase;
                int hi = address >> 8;
                if ((hi < 0x40) || (hi > 0x5A)) continue;

                if (pageBuckets[hi] == null) pageBuckets[hi] = new List<Difference>();
                pageBuckets[hi].Add(differences[i]);
            }

            for (int hi = 0x40; hi <= 0x5A; hi++)
            {
                if (pageBuckets[hi] == null) continue;
                AddSamePageOrNormalV2(output, hi, pageBuckets[hi]);
            }
        }

        private static void AddRawRunsV2(List<byte> output, List<Difference> differences, bool[] used, int startIndex, int runLength)
        {
            int current = startIndex;
            int remaining = runLength;

            while (remaining >= 3)
            {
                int chunkLength = Math.Min(255, remaining);
                AddRawRunV2(output, differences, current, chunkLength);

                for (int i = 0; i < chunkLength; i++)
                {
                    used[current + i] = true;
                }

                current += chunkLength;
                remaining -= chunkLength;
            }
        }

        private static void AddRawRunV2(List<byte> output, List<Difference> differences, int startIndex, int length)
        {
            int address = differences[startIndex].Index + ScreenBase;
            output.Add(CommandRawRun);
            output.Add((byte)(address >> 8));
            output.Add((byte)(address & 0xFF));
            output.Add((byte)length);

            for (int i = 0; i < length; i++)
            {
                output.Add(differences[startIndex + i].Value2);
            }
        }

        private static void AddSamePageOrNormalV2(List<byte> output, int hi, List<Difference> pageDifferences)
        {
            int index = 0;
            while (pageDifferences.Count - index >= 3)
            {
                int chunkLength = Math.Min(255, pageDifferences.Count - index);
                output.Add((byte)(hi + 128));
                output.Add((byte)chunkLength);

                for (int i = 0; i < chunkLength; i++)
                {
                    Difference difference = pageDifferences[index + i];
                    output.Add((byte)((difference.Index + ScreenBase) & 0xFF));
                    output.Add(difference.Value2);
                }

                index += chunkLength;
            }

            while (index < pageDifferences.Count)
            {
                AddNormalWriteV2(output, pageDifferences[index]);
                index++;
            }
        }

        private static void AddNormalWriteV2(List<byte> output, Difference difference)
        {
            int address = difference.Index + ScreenBase;
            output.Add((byte)(address >> 8));
            output.Add((byte)(address & 0xFF));
            output.Add(difference.Value2);
        }

        static byte[] AddBytes(byte[] originalArray, params byte[] bytesToAdd)
        {
            // Create a new array with enough space for the existing and new bytes
            byte[] newArray = new byte[originalArray.Length + bytesToAdd.Length];

            // Copy the existing bytes to the new array
            Array.Copy(originalArray, newArray, originalArray.Length);

            // Copy the new bytes to the new array
            Array.Copy(bytesToAdd, 0, newArray, originalArray.Length, bytesToAdd.Length);

            return newArray;
        }

        private void MoveItem(int direction)
        {
            // Check if an item is selected and if it can be moved in the specified direction
            if (listBox1.SelectedItem != null && listBox1.SelectedIndex + direction >= 0 && listBox1.SelectedIndex + direction < listBox1.Items.Count)
            {
                // Swap the items in the ListBox
                int newIndex = listBox1.SelectedIndex + direction;
                object selected = listBox1.SelectedItem;
                listBox1.Items.RemoveAt(listBox1.SelectedIndex);
                listBox1.Items.Insert(newIndex, selected);
                listBox1.SelectedIndex = newIndex;
            }
        }

        private byte[] ReadBytesFromFile(string filePath, int numBytes)
        {
            // Read the specified number of bytes from the file
            byte[] byteArray;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byteArray = new byte[numBytes];
                fs.Read(byteArray, 0, numBytes);
            }

            return byteArray;
        }

        private void ViewSCR(string fileName)
        {
            byte[] screen = ReadBytesFromFile(fileName, ScreenSize);
            Bitmap bitmap = new Bitmap(256, 192);

            for (int y = 0; y < 192; y++)
            {
                int bitmapRowOffset = ((y & 0xC0) << 5) | ((y & 0x07) << 8) | ((y & 0x38) << 2);
                int attributeRowOffset = 6144 + ((y >> 3) * 32);

                for (int xByte = 0; xByte < 32; xByte++)
                {
                    byte pixels = screen[bitmapRowOffset + xByte];
                    byte attribute = screen[attributeRowOffset + xByte];
                    bool bright = (attribute & 0x40) != 0;
                    Color ink = SpectrumColors[(attribute & 0x07) + (bright ? 8 : 0)];
                    Color paper = SpectrumColors[((attribute >> 3) & 0x07) + (bright ? 8 : 0)];

                    for (int bit = 0; bit < 8; bit++)
                    {
                        bool pixelSet = (pixels & (0x80 >> bit)) != 0;
                        bitmap.SetPixel((xByte * 8) + bit, y, pixelSet ? ink : paper);
                    }
                }
            }

            Image oldImage = pictureBox1.Image;
            pictureBox1.Image = bitmap;
            if (oldImage != null) oldImage.Dispose();
            pictureBox1.Visible = true;
            pictureBox1.BringToFront();
        }

        private void pickfiles()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Allow selecting multiple files
            openFileDialog.Multiselect = true;

            // Set the title of the dialog
            openFileDialog.Title = "Select Files";

            // Set the initial directory (optional)
            //openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Set the file types allowed (optional)
            openFileDialog.Filter = "ZX Spectrum Screen Files (*.scr)|*.SCR|All Files (*.*)|*.*";

            // Show the dialog and get the result
            DialogResult result = openFileDialog.ShowDialog();

            // Check if the user clicked OK
            if (result == DialogResult.OK)
            {
                // Get the selected file names (for multiple files)
                string[] selectedFiles = openFileDialog.FileNames;

                // Display the selected files
                Console.WriteLine("Selected Files:");
                foreach (string file in selectedFiles)
                {
                    listBox1.Items.Add(file);
                }
            }
            else
            {
                //Console.WriteLine("File selection canceled.");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MoveItem(-1);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            MoveItem(1);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            PackV2();

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null) return;
            ViewSCR(listBox1.SelectedItem.ToString());
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Image oldImage = pictureBox1.Image;
            pictureBox1.Image = null;
            if (oldImage != null) oldImage.Dispose();
            pictureBox1.Visible = false;
        }

        static List<Difference> FindDifferences(byte[] array1, byte[] array2)
        {
            List<Difference> differences = new List<Difference>();

            // Check if the arrays are of the same length
            if (array1.Length != array2.Length)
            {
                //Console.WriteLine("Arrays have different lengths.");
                return differences;
            }

            // Iterate through each byte and compare
            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    differences.Add(new Difference { Index = i, Value1 = array1[i], Value2 = array2[i] });
                }
            }

            return differences;
        }

        // Class to represent a difference
        class Difference
        {
            public int Index { get; set; }
            public byte Value1 { get; set; }
            public byte Value2 { get; set; }
        }
    }
}
