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
        public Form1()
        {
            InitializeComponent();
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
            Pack();
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
