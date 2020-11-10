using Argos.Base;
using Argos.Base.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SavixSim
{
    public class StakingSim
    {
        public class SupplyMapProxyPoint
        {
            public Int64 Days { get; set; }
            public Int64 TotalSupply { get; set; }
        }

        public class DataPoint
        {
            public DateTime Date { get; set; }
            public double Days { get; set; }
            public BigInteger Xt { get; set; }
            public BigInteger X1 { get; set; }
            public BigInteger X2 { get; set; }
            public BigInteger Y1 { get; set; }
            public BigInteger Y2 { get; set; }
            public BigInteger Gradient { get; set; }
            public BigInteger TotalSupply { get; set; }
            public BigInteger TotalSupplyShift { get; set; }
            public BigInteger InterestSinceLastAdjust { get; set; }
            public BigInteger InterestPerDay { get; set; }
        }

        private bool _debug = false;
        private BigInteger _maxBigInt = BigInteger.Pow(2, 256) - 1;
        private BigInteger _maxInt = BigInteger.Pow(2, 128) - 1;
        private const int _decimals = 9;
        private const int _secperday = 3600 * 24;
        private int _minTimeWin = 60;
        private int _constInterest = 8;
        private BigInteger _constGradient = 25 * Convert.ToInt64(Math.Pow(10, _decimals - 4));
        public Int64[] TestTimeStamps = new Int64[]
        {
                    59,
                    60,
                    3 * _secperday,
                    8 * _secperday,
                    30 * _secperday,
                    50 * _secperday,
                    (6 * 30 * _secperday) - 1,
                    (6 * 30 * _secperday) + 1,
                    11 * 30 * _secperday,
                    17 * 30 * _secperday,
                    18 * 30 * _secperday,
                    (24 * 30 * _secperday) - 1,
                    48 * 30 * _secperday,
                    49 * 30 * _secperday
        };

        public BigInteger[,] SupplyMap = new BigInteger[,]
        {
                    {0, 100000},
                    {7 * _secperday, 115000},
                    {30 * _secperday, 130000},
                    {6 * 30 * _secperday, 160000},
                    {12 * 30 * _secperday, 185000},
                    {18 * 30 * _secperday, 215000},
                    {24 * 30 * _secperday, 240000},
                    {48 * 30 * _secperday, 300000}
        };

        // internal runtime variables
        private Int64 _secLastAdjustment = 0;
        private BigInteger _lastGradient = 1;
        private Int64 _secStartStaking = 0;
        private BigInteger _initialSupply = 0;
        private BigInteger _totalSupply = 0;
        private BigInteger _lastTotalSupply = 0;

        // test variables
        private string _outfilename = Environment.CurrentDirectory + "\\" + "stakingdata.csv";
        private int _testIterations = 200;
        private Int64 _testTimeTrigger = 0;

        public List<DataPoint> Data = new List<DataPoint>();

        public static long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return (long)Math.Floor(diff.TotalSeconds);
        }

        public static DateTime ConvertFromUnixTimestamp(long timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }

        private static BigInteger UpShiftDecimals(Int64 lowNum)
        {
            BigInteger highNum = (BigInteger)Convert.ToDecimal(lowNum * Math.Pow(10, _decimals));
            return highNum;
        }

        private static BigInteger UpShiftDecimals(double lowNum)
        {
            BigInteger highNum = (BigInteger)Convert.ToDecimal(lowNum * Math.Pow(10, _decimals));
            return highNum;
        }

        private static Int64 DownShiftDecimals(BigInteger highNum)
        {
            Int64 lowNum = Convert.ToInt64((double)(highNum / (BigInteger)Convert.ToDecimal(Math.Pow(10, _decimals))));
            return lowNum;
        }

        public void StartStaking(Boolean upshift = true)
        {
            if (_debug) Log.Write("START", "initializeSupplyMap");

            if (upshift)
            {
                for (int i = 0; i <= SupplyMap.GetUpperBound(0); i++)
                {
                    SupplyMap[i, 1] = UpShiftDecimals((Int64)SupplyMap[i, 1]);
                }
            }
            // _constGradient = Convert.ToInt64((((Decimal)SupplyMap[SupplyMap.GetUpperBound(0), 1]/100) * _constInterest) / (360 * _secperday));
            _constGradient = (BigInteger)(((SupplyMap[0, 1] / 100) * _constInterest) / (360 * _secperday));

            _initialSupply = SupplyMap[0, 1];
            _totalSupply = _initialSupply;
            _lastTotalSupply = _initialSupply;
            _secStartStaking = ConvertToUnixTimestamp(DateTime.Now);
            _secLastAdjustment = 0;
            _testTimeTrigger = 0;
            _lastGradient = UpShiftDecimals(1);

            Log.Write("Setting initial supply to " + DownShiftDecimals(_initialSupply).ToString(), "initializeSupplyMap");
            Log.Write("Constant gradient after " + ((Decimal)SupplyMap[SupplyMap.GetUpperBound(0), 0] / (360 * _secperday)).ToString() + " years: " + _constGradient.ToString(), "initializeSupplyMap");
            Log.Write("Constant interest after " + ((Decimal)SupplyMap[SupplyMap.GetUpperBound(0), 0] / (360 * _secperday)).ToString() + " years: " + _constInterest.ToString() + "% of " + DownShiftDecimals(SupplyMap[0, 1]).ToString(), "initializeSupplyMap");
        }

        private  BigInteger[] GetTimeWindow(Int64 secSinceStart)
        {
            BigInteger X1 = 0;
            BigInteger X2 = 0;
            for (int i = 0; i <= SupplyMap.GetUpperBound(0); i++)
            {
                if (SupplyMap[i, 0] == 0) continue;
                if (secSinceStart < SupplyMap[i, 0])
                {
                    X2 = SupplyMap[i, 0];
                    // if (_debug) Log.Write("secSinceStart: " + secSinceStart.ToString() + ", X1: " + X1.ToString() + ", X2: " + X2.ToString(), "nextMappingPoint");
                    break;
                }
                else
                    X1 = SupplyMap[i, 0];
            }
            if (X2 == 0) X2 = _maxBigInt;
            BigInteger[] timeWindow = { X1, X2 };
            return timeWindow;
        }

        private BigInteger[] GetSupplyWindow(Int64 secSinceStart)
        {
            BigInteger Y1 = _initialSupply;
            BigInteger Y2 = 0;

            for (int i = 0; i <= SupplyMap.GetUpperBound(0); i++)
            {
                if (SupplyMap[i, 0] == 0) continue;
                if (secSinceStart < SupplyMap[i, 0])
                {
                    Y2 = SupplyMap[i, 1];
                    break;
                }
                else
                    Y1 = SupplyMap[i, 1];
            }
            if (Y2 == 0) Y2 = _maxInt;
            BigInteger[] supplyWin = { Y1, Y2 };
            return supplyWin;
        }

        private void AdjustSupply(Int64 calcTime, ref int outputCounter, int outputReduceFactor = 0)
        {
            if (calcTime - _secLastAdjustment < _minTimeWin)
            {
                if (_debug) Log.Write("Adjustment is possible once per " + _minTimeWin.ToString() + " seconds only.", "Warning");
                return;
            }

            BigInteger gradient;
            BigInteger _addSupply;
            if (calcTime >= SupplyMap[SupplyMap.GetUpperBound(0), 0])
            {
                gradient = _constGradient;
                _addSupply = gradient * (calcTime - SupplyMap[SupplyMap.GetUpperBound(0), 0]);
                _totalSupply = SupplyMap[SupplyMap.GetUpperBound(0), 1] + _addSupply;

                outputCounter++;
                if (_debug) Log.Write("_secLastAdjustment: " + _secLastAdjustment.ToString() + " -> _totalSupply = " + _totalSupply.ToString(), "AdjustSupply");
                if (outputReduceFactor == 0 || (outputCounter % outputReduceFactor) == 0)
                    OutputAdjustmentToJson(calcTime, SupplyMap[SupplyMap.GetUpperBound(0), 0], _maxBigInt, SupplyMap[SupplyMap.GetUpperBound(0), 1], _maxInt, gradient, _totalSupply, _secLastAdjustment, _lastTotalSupply);

                _secLastAdjustment = calcTime;
                _lastGradient = gradient;
                _lastTotalSupply = _totalSupply;
                return;
            }
            BigInteger[] timeWin = GetTimeWindow(calcTime);
            BigInteger[] supplyWin = GetSupplyWindow(calcTime);
            gradient = (BigInteger)((supplyWin[1] - supplyWin[0]) / (timeWin[1] - timeWin[0]));
            _addSupply = (BigInteger)gradient * (calcTime - timeWin[0]);
            _totalSupply = supplyWin[0] + _addSupply;

            outputCounter++;
            if (_debug) Log.Write("_secLastAdjustment: " + _secLastAdjustment.ToString() + " -> _totalSupply = " + _totalSupply.ToString(), "AdjustSupply");
            if (outputReduceFactor == 0 || (outputCounter % outputReduceFactor) == 0)
                OutputAdjustmentToJson(calcTime, timeWin[0], timeWin[1], supplyWin[0], supplyWin[1], gradient, _totalSupply, _secLastAdjustment, _lastTotalSupply);

            _secLastAdjustment = calcTime;
            _lastGradient = gradient;
            _lastTotalSupply = _totalSupply;
            return;
        }

        private void OutputAdjustmentToJson(BigInteger adjustTime, BigInteger X1, BigInteger X2, BigInteger Y1, BigInteger Y2, BigInteger Gradient, BigInteger TotalSupply, BigInteger SecLastChange, BigInteger LastSupply)
        {
            double DaysSinceStart = ((double)adjustTime / (3600 * 24));
            JsonObject json = new JsonObject();
            json["DateTime"] = ConvertFromUnixTimestamp((long)adjustTime + _secStartStaking).ToString("yyyy-MM-dd HH:mm:ss");
            json["Days"] = DaysSinceStart.ToString().Replace(".", ",");
            json["Xt"] = adjustTime;
            json["X1"] = X1;
            json["X2"] = X2;
            json["Y1"] = Y1;
            json["Y2"] = Y2;
            json["Gradient"] = Gradient;
            json["TotalSupply"] = TotalSupply;
            BigInteger InterestSinceLastAdjust = BigInteger.Divide((TotalSupply - LastSupply) * 100 * BigInteger.Pow(10, _decimals), LastSupply);
            BigInteger InterestPerDay = BigInteger.Divide(InterestSinceLastAdjust * _secperday, adjustTime - SecLastChange);
            json["InterestPerDay"] = InterestPerDay;
            Utility.WriteJsonToCsv(json, _outfilename, "Error", false);
            Data.Add(CreateDatapoint(adjustTime, X1, X2, Y1, Y2, Gradient, _totalSupply, InterestSinceLastAdjust, InterestPerDay));
        }
        public void RunRandomTest(int iterations, int timeIntervallLowEnd, int timeIntervallHighEnd, int outputReduceFactor = 0)
        {
            Log.Write("Start test with " + iterations.ToString() + " iterations at random intervals between " + timeIntervallLowEnd.ToString() + " and " + timeIntervallHighEnd.ToString() + " seconds.", "runRandomTest");
            Random rnd = new Random();
            int outputCounter = 0;

            for (int i = 0; i < iterations; i++)
            {
                int timeIntervall = rnd.Next(timeIntervallLowEnd, timeIntervallHighEnd);
                _testTimeTrigger = _testTimeTrigger + timeIntervall;
                AdjustSupply(_testTimeTrigger, ref outputCounter, outputReduceFactor);
            }
            Log.Write(outputCounter.ToString() + " test cycles finished.", "runRandomTest");
            Log.Write("--------------------------------------------------------", "runRandomTest");
            return;
        }
        public void RunStandardTest(int outputReduceFactor = 0)
        {
            Log.Write("Start test standard.", "runStandardTest");
            int outputCounter = 0;

            for (int i = 0; i < TestTimeStamps.Length; i++)
            {
                if (_debug)
                {
                    Log.Write("-----------------------------------------------------", "runStandardTest");
                    Log.Write("Test " + i.ToString() + ": ", "runStandardTest");
                    Log.Write("_secLastAdjustment: " + _secLastAdjustment.ToString(), "runStandardTest");
                    Log.Write("testTimeStamps[" + i.ToString() + "]: " + TestTimeStamps[i].ToString(), "runStandardTest");
                }
                AdjustSupply(TestTimeStamps[i], ref outputCounter, outputReduceFactor);
            }
            Log.Write(outputCounter.ToString() + " test cycles finished.", "runStandardTest");
            Log.Write("--------------------------------------------------------", "runStandardTest");
            return;
        }
        private void TestGetTimeWindow(string timestamps)
        {
            string[] points = timestamps.Split(',');
            foreach (string t in points)
            {
                if (String.IsNullOrEmpty(t)) continue;
                BigInteger[] tWin = GetTimeWindow(Convert.ToInt64(t.Trim()));
                Log.Write(t.Trim() + " -> X1 = " + tWin[0].ToString() + ", X2 = " + tWin[1].ToString(), "TimeWindow");
            }
        }
        private void TestGetSupplyWindow(string timestamps)
        {
            string[] points = timestamps.Split(',');
            foreach (string t in points)
            {
                if (String.IsNullOrEmpty(t)) continue;
                BigInteger[] tWin = GetSupplyWindow(Convert.ToInt64(t.Trim()));
                Log.Write(t.Trim() + " -> X1 = " + tWin[0].ToString() + ", X2 = " + tWin[1].ToString(), "SupplyWindow");
            }
        }
    
        // Data.Add(CreateDatapoint(calcTime, (Int64)SupplyMap[SupplyMap.GetUpperBound(0), 0], Int64.MaxValue, DownShiftDecimals(SupplyMap[SupplyMap.GetUpperBound(0), 1]), Int64.MaxValue, gradient, DownShiftDecimals(_addSupply), DownShiftDecimals(_totalSupply), _secLastAdjustment, DownShiftDecimals(_lastTotalSupply)));

        private DataPoint CreateDatapoint(BigInteger adjustTime, BigInteger X1, BigInteger X2, BigInteger Y1, BigInteger Y2, BigInteger Gradient, BigInteger TotalSupply, BigInteger InterestSinceLastAdjust, BigInteger InterestPerDay)
        {
            double DaysSinceStart = ((double)adjustTime / (3600 * 24));
            DataPoint dp = new DataPoint();
            dp.Date = ConvertFromUnixTimestamp((long)adjustTime + _secStartStaking);
            dp.Days = DaysSinceStart;
            dp.Xt = adjustTime;
            dp.X1 = X1;
            dp.X2 = X2;
            dp.Y1 = Y1;
            dp.Y2 = Y2;
            dp.Gradient = Gradient;
            dp.TotalSupply = TotalSupply;
            dp.TotalSupplyShift = DownShiftDecimals(TotalSupply);
            dp.InterestSinceLastAdjust = InterestSinceLastAdjust;
            dp.InterestPerDay = InterestPerDay;
            return dp;
        }
    }
}
