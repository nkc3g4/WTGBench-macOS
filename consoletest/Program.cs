using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Text;
using System.Buffers.Binary;

static void GenerateRandomArray(byte[] rndArr)
{
    Random random = new Random(123456);
    for (int i = 0; i < rndArr.Length; i++)
    {
        rndArr[i] = (byte)random.Next(255);
    }
}

static void WriteCsv(string fileName, List<double> resultList)
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("i,val,");
    sb.AppendJoin(Environment.NewLine, resultList.Select((v, idx) => idx.ToString() + "," + v.ToString() + ","));
    File.WriteAllText(fileName, sb.ToString());
}

static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
{
    return a1.SequenceEqual(a2);
}

DriveInfo[] driveInfos = DriveInfo.GetDrives().Where(x => !x.RootDirectory.FullName.StartsWith("/System")).ToArray();
for (int i = 0; i < driveInfos.Length; i++)
{
    Console.WriteLine("{0}: {1} ({2} MiB Free)", i, driveInfos[i].RootDirectory.FullName, driveInfos[i].AvailableFreeSpace / (1024 * 1024));
}
Console.WriteLine("Input Volume Index:");
string userSel = Console.ReadLine();
int diskIndex = -1;
if (!int.TryParse(userSel, out diskIndex))
{
    return;
}

var testFilePath = Path.Join(driveInfos[diskIndex].RootDirectory.FullName, "test.bin");
if (File.Exists(testFilePath))
{
    File.Delete(testFilePath);
}
SafeFileHandle safeFileHandle = File.OpenHandle(testFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.WriteThrough);
FileStream fileStream = new FileStream(safeFileHandle, FileAccess.ReadWrite, 0, false);
const int blockSize = 1024 * 1024;
byte[] buffer = new byte[blockSize];
byte[] readBuffer = new byte[blockSize];
GenerateRandomArray(buffer);
var maxPos = driveInfos[diskIndex].AvailableFreeSpace - 100 * 1024 * 1024;
var maxNum = (driveInfos[diskIndex].AvailableFreeSpace - 100 * 1024 * 1024) / blockSize;
Stopwatch stopwatch = new Stopwatch();
var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
int num = 0;
bool isStart = false;
List<double> speedList = new List<double>();

new Task(async () =>
{
    var previousTime = stopwatch.ElapsedMilliseconds;
    var previousNum = 0;
    var previousFsPos = fileStream.Position;

    while (await timer.WaitForNextTickAsync())
    {
        var curTime = stopwatch.ElapsedMilliseconds;
        var curNum = num;
        var curPos = fileStream.Position;
        if (isStart && curTime - previousTime > 900)
        {
            var speed = (curPos - previousFsPos) / (1024.0 * 1024);
            speedList.Add(speed);
            Console.Write("\r {0} MiB/s {1} %    ", Math.Round(speed,2), Math.Round((curPos / (float)maxPos) * 100,2));
            previousTime = curTime;
            previousNum = curNum;
            previousFsPos = curPos;
        }
        else
        {
            previousFsPos = fileStream.Position;
        }
    }

}).Start();

Console.WriteLine("Start Sequential Writing!");
stopwatch.Start();
isStart = true;
for (; num < maxNum&&fileStream.Position< maxPos; num++)
{
    fileStream.Position += blockSize;
    fileStream.Write(buffer, 0, blockSize);

}
isStart = false;
Console.Write("\r {0} MiB/s {1} %    ", Math.Round(speedList.Last(), 2), "100");

Console.WriteLine();
Console.WriteLine("Write Finish");
Console.WriteLine("Write Speed Average: {0} MiB/s", Math.Round(speedList.Average(),2));

WriteCsv("writeStat.csv", speedList);
fileStream.Position = 0L;
speedList.Clear();
Thread.Sleep(2000);
Console.WriteLine("Start Sequential Reading");
isStart = true;
var verified = true;
for(; fileStream.Position < maxPos;)
{
    fileStream.Position += blockSize;
    fileStream.Read(readBuffer, 0, blockSize);
    if(!ByteArrayCompare(readBuffer, buffer))
    {
        verified = false;
        Console.WriteLine("Read Error on: {0}!!!",fileStream.Position);
    }
}
isStart = false;
Console.Write("\r {0} MiB/s {1} %    ", Math.Round(speedList.Last(), 2), "100");
Console.WriteLine();
Console.WriteLine("Read Finish");
Console.WriteLine("Read Speed Average: {0} MiB/s", Math.Round(speedList.Average(), 2));
if (verified)
{
    Console.WriteLine("Verified: OK!");
}
WriteCsv("readStat.csv", speedList);
Console.WriteLine("Press any key to exit!");
timer.Dispose();

Console.ReadKey();
