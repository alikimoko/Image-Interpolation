using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ImageInterpolation
{
    class Program
    {
        enum Orientation { Horizontal, Vertical, Loose, Undefined }

        static Orientation orientation = Orientation.Undefined;
        static int last = 0, next = 0, current = 0,
                   width, height, size;
        static List<int[]> frames;

        static void Main()
        {
            string next;
            frames = new List<int[]>();

            Console.WriteLine("\nImages must be the same size\nEnter the first filename...");
            while (true)
            {
                next = Console.ReadLine();
                try
                {
                    Bitmap bmp = new Bitmap(next);
                    width = bmp.Width;
                    height = bmp.Height;
                    size = width * height;
                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    int[] frame = new int[size];
                    Marshal.Copy(data.Scan0, frame, 0, width * height);
                    frames.Add(frame);
                    bmp.UnlockBits(data);
                    bmp.Dispose();
                    break;
                }
                catch
                {
                    Console.WriteLine("Could not open file: " + next + "\nPlease check for typos");
                }
            }

            while (true)
            {
                Console.WriteLine("\nEnter another file or end...\nFormat: %filename% %frames until next file%");
                next = Console.ReadLine();
                if (next == "end")
                    break;

                while (true)
                {
                    string[] split = next.Split(' ');
                    try
                    {
                        int newframes = int.Parse(split[split.Length - 1]);
                        next = next.Substring(0, next.LastIndexOf(' '));
                        if (newframes < 1)
                            throw new ArgumentException("Frames until next file must be at least 1");
                        try
                        {
                            Bitmap bmp = new Bitmap(next);
                            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                            int[] frame = new int[size];
                            Marshal.Copy(data.Scan0, frame, 0, width * height);
                            Interpolate(newframes, frame);
                            frames.Add(frame);
                            bmp.UnlockBits(data);
                            bmp.Dispose();
                            break;
                        }
                        catch (FileNotFoundException)
                        {
                            Console.WriteLine("Could not open \"" + next + "\"\nPlease try again...");
                            next = Console.ReadLine();
                        }
                        catch (Exception e)
                        {
                            string m = e.Message;
                            Console.WriteLine("Something went wrong\nDoes the image have the same dimension as the first one?\nDid you use the right extention?\nPlease try again...");
                            next = Console.ReadLine();
                        }
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Could not parse \"" + split[split.Length - 1] + "\" as a valid number\nPlease try again...");
                        next = Console.ReadLine();
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("Frames until next file must be at least 1\nPlease try again...");
                        next = Console.ReadLine();
                    }
                    catch
                    {
                        Console.WriteLine("Something went wrong\nPlease try again...");
                        next = Console.ReadLine();
                    }
                }
            }

            Console.WriteLine("\nCreated a total of " + Program.next + 1 + " frames\nHow should they be stitched?\nHorizontal: 0, h, hor, horizontal (case insensitive)\nVertical: 1, v, ver, vertical (case insensitive)\nWrite each frame seperate instead: 2, none, loose, seperate (case insensitive)");
            while (orientation == Orientation.Undefined)
            {
                next = Console.ReadLine().ToLower();
                switch (next)
                {
                    case "0": case "h": case "hor": case "horizontal": orientation = Orientation.Horizontal; break;
                    case "1": case "v": case "ver": case "vertical": orientation = Orientation.Vertical; break;
                    case "2": case "none": case "loose": case "seperate": orientation = Orientation.Loose; break;
                    default: Console.WriteLine(next + " is not a valid orientation\nPlease try again..."); break;
                }
            }

            if (orientation == Orientation.Loose)
            {
                Console.WriteLine("\nHow should the frames be saved?\nDon't use an extention...");
                while (true)
                {
                    next = Console.ReadLine();
                    try
                    {
                        Bitmap bmp = new Bitmap(width, height);
                        for (int i = 0; i < frames.Count; i++)
                        {
                            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                            Marshal.Copy(frames[i], 0, data.Scan0, size);
                            bmp.UnlockBits(data);
                            bmp.Save(next + i + ".png", ImageFormat.Png);
                        }
                        bmp.Dispose();
                        return;
                    }
                    catch
                    {
                        Console.WriteLine("Could not save to \"" + next + "\"\nPlease try a different location");
                    }
                }
            }
            else
            {
                int[] output = Stitch();
                Bitmap bmp;
                BitmapData data;
                if (orientation == Orientation.Horizontal)
                {
                    bmp = new Bitmap(width * frames.Count, height);
                    data = bmp.LockBits(new Rectangle(0, 0, width * frames.Count, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                }
                else
                {
                    bmp = new Bitmap(width, height * frames.Count);
                    data = bmp.LockBits(new Rectangle(0, 0, width, height * frames.Count), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                }
                Marshal.Copy(output, 0, data.Scan0, size * frames.Count);
                bmp.UnlockBits(data);
                Console.WriteLine("Where should the strip be saved?");
                while (true)
                {
                    next = Console.ReadLine();
                    try
                    {
                        bmp.Save(next);
                        return;
                    }
                    catch
                    {
                        Console.WriteLine("Could not save to " + next + "\nPlease try a different location");
                    }
                }
            }
        }

        static void Interpolate(int frames, int[] frame)
        {
            last = next;
            next = last + frames;
            current = last + 1;
            int[] lastFrame = Program.frames[last];
            Vector4[] lastColors = new Vector4[frame.Length], nextColors = new Vector4[frame.Length];
            for (int i = 0; i < frame.Length; i++)
            {
                lastColors[i] = FromInt(lastFrame[i]);
                nextColors[i] = FromInt(frame[i]);
            }
            lastFrame = null;
            while (current < next)
            {
                float part = (current - last) / (float)frames;
                int[] nextFrame = new int[frame.Length];
                for (int i = 0; i < frame.Length; i++)
                    nextFrame[i] = FromVector(Vector4.Lerp(lastColors[i], nextColors[i], part));
                Program.frames.Add(nextFrame);
                current++;
            }
        }

        static int[] Stitch()
        {
            int total = frames.Count;
            if (orientation == Orientation.Horizontal)
            {
                int[][] stripData = new int[height][];
                for (int i = 0; i < height; i++)
                {
                    stripData[i] = new int[width * total];
                    for(int j = 0; j < total; j++)
                        Array.Copy(frames[j], i * width, stripData[i], j * width, width);
                }
                int[] strip = new int[size * total];
                for (int i = 0; i < height; i++)
                    Array.Copy(stripData[i], 0, strip, width * i * total, width * total);
                return strip;
            }
            else
            {
                int[] strip = new int[size * total];
                for (int j = 0; j < total; j++)
                    Array.Copy(frames[j], 0, strip, size * j, size);
                return strip;
            }
        }

        static Vector4 FromInt(int val)
        {
            return new Vector4(val & 0xff, (val >> 8) & 0xff, (val >> 16) & 0xff, (val >> 24) & 0xff);
        }

        static int FromVector(Vector4 val)
        {
            return (int)Math.Round(val.X) + ((int)Math.Round(val.Y) << 8) + ((int)Math.Round(val.Z) << 16) + ((int)Math.Round(val.W) << 24);
        }
    }
}
