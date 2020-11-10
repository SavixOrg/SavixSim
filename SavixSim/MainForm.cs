using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;

using System.Collections;
using System.Numerics;
using DevExpress.XtraCharts;

namespace SavixSim
{
    public partial class MainForm : DevExpress.XtraEditors.XtraForm
    {

        StakingSim _sim = new StakingSim();
        List<StakingSim.SupplyMapProxyPoint> supplyMapProxy = new List<StakingSim.SupplyMapProxyPoint>();

        public MainForm()
        {
            InitializeComponent();

            textEditIterations.Text = "12000000";
            textEditIntervalLow.Text = "1";
            textEditIntervalHigh.Text = "30";
            textEditOutputReduceFactor.Text = "1000";
            radioGroupTest.EditValue = "Standard Test";
            UpdateLayout();

            for (int i = 0; i <= _sim.SupplyMap.GetUpperBound(0); i++)
            {
                StakingSim.SupplyMapProxyPoint mp = new StakingSim.SupplyMapProxyPoint();
                mp.Days = (Int64)_sim.SupplyMap[i, 0] / 86400;
                mp.TotalSupply = (Int64)_sim.SupplyMap[i, 1];
                supplyMapProxy.Add(mp);
            }

            gridControlSupplyMap.DataSource = supplyMapProxy;
            gridControlTimestamps.DataSource = _sim.TestTimeStamps;
            gridViewTimestamps.Columns[0].Caption = "Test Timestamps (in Seconds)";
        }

        public Series CreateTotalSupplySeries(List<StakingSim.DataPoint> data, ChartControl chart)
        {
            // Create a line series.
            Series series = new Series("Series 1", ViewType.Line);

            series.Points.Add(new SeriesPoint(0, supplyMapProxy[0].TotalSupply));
            foreach (var p in data)
                series.Points.Add(new SeriesPoint(p.Days, (Int64)(p.TotalSupplyShift)));

            // Set the numerical argument scale types for the series,
            // as it is qualitative, by default.
            series.ArgumentScaleType = ScaleType.Numerical;

            chart.Series.Add(series);

            // Access the view-type-specific options of the series.
            ((LineSeriesView)series.View).MarkerVisibility = DevExpress.Utils.DefaultBoolean.False;
            ((LineSeriesView)series.View).LineStyle.DashStyle = DashStyle.Solid;
            ((LineSeriesView)series.View).AxisY.VisualRange.MinValue = supplyMapProxy[0].TotalSupply;
            ((LineSeriesView)series.View).AxisY.WholeRange.MinValue = supplyMapProxy[0].TotalSupply;
            ((LineSeriesView)series.View).AxisX.WholeRange.MinValue = -10;

            return series;
        }

        public Series CreateSupplyMapSeries(ChartControl chart)
        {
            // Create a line series.
            Series series = new Series("Series 1", ViewType.Line);

            series.Points.Add(new SeriesPoint(0, supplyMapProxy[0].TotalSupply));
            foreach (var p in supplyMapProxy)
                series.Points.Add(new SeriesPoint(p.Days, p.TotalSupply));

            // Set the numerical argument scale types for the series,
            // as it is qualitative, by default.
            series.ArgumentScaleType = ScaleType.Numerical;

            chart.Series.Add(series);

            // Access the view-type-specific options of the series.
            ((LineSeriesView)series.View).MarkerVisibility = DevExpress.Utils.DefaultBoolean.False;
            ((LineSeriesView)series.View).LineStyle.DashStyle = DashStyle.Solid;
            ((LineSeriesView)series.View).AxisY.VisualRange.MinValue = supplyMapProxy[0].TotalSupply;
            ((LineSeriesView)series.View).AxisY.WholeRange.MinValue = supplyMapProxy[0].TotalSupply;
            ((LineSeriesView)series.View).AxisX.WholeRange.MinValue = -10;

            return series;
        }

        private void simpleButtonStart_Click(object sender, EventArgs e)
        {
            gridControl1.DataSource = _sim.Data;


            for (int i = 0; i <= _sim.SupplyMap.GetUpperBound(0); i++)
            {
                _sim.SupplyMap[i,0] = supplyMapProxy[i].Days * 86400;
                _sim.SupplyMap[i,1] = supplyMapProxy[i].TotalSupply;
            }

            _sim.StartStaking();
            if (Convert.ToString(radioGroupTest.EditValue) == "Standard Test")
            {
                _sim.RunStandardTest();
            }
            else
            {
                int testIterations = Convert.ToInt32(textEditIterations.Text);
                int intervalLow = Convert.ToInt32(textEditIntervalLow.Text);
                int intervalHigh = Convert.ToInt32(textEditIntervalHigh.Text);
                int reduceFactor = Convert.ToInt32(textEditOutputReduceFactor.Text);
                _sim.RunRandomTest(testIterations, intervalLow, intervalHigh, reduceFactor);
            }
            gridControl1.RefreshDataSource();

            chartControl1.Series.Clear();
            CreateTotalSupplySeries(_sim.Data, chartControl1);
            CreateSupplyMapSeries(chartControl1);

            // Access the type-specific options of the diagram.
            ((XYDiagram)chartControl1.Diagram).EnableAxisXZooming = true;
            ((XYDiagram)chartControl1.Diagram).EnableAxisXScrolling = true;

            ((XYDiagram)chartControl1.Diagram).EnableAxisYZooming = true;
            ((XYDiagram)chartControl1.Diagram).EnableAxisYScrolling = true;

            // Hide the legend (if necessary).
            chartControl1.Legend.Visibility = DevExpress.Utils.DefaultBoolean.False;

            if (chartControl1.Titles.Count == 0)
            {
                // Add a title to the chart (if necessary).
                chartControl1.Titles.Add(new ChartTitle());
                chartControl1.Titles[0].Text = "Staking Progression";
            }
        }

        private void gridViewSupplyMap_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {

        }

        private void gridViewSupplyMap_CellValueChanging(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
        }

        public void UpdateLayout()
        {
            if (Convert.ToString(radioGroupTest.EditValue) == "Standard Test")
            {
                layoutControlGroupRandom.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
                layoutControlItemTimestamps.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
                splitterItemTimestamps.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
            }
            else
            {
                layoutControlGroupRandom.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Always;
                layoutControlItemTimestamps.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
                splitterItemTimestamps.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
            }
        }
        private void radioGroupTest_EditValueChanged(object sender, EventArgs e)
        {
            UpdateLayout();
        }
    }
}
