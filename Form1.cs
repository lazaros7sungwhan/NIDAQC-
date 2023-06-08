using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using NationalInstruments.DAQmx; //이거 하나만 있으면 Ni Daq 됨
using System.Threading; // Threadsleep 및 쓰레드생성에 필요
using System.IO; // 데이터 저장 (StreamReader 등에 필요)
using System.Diagnostics; // StopWatch 에 필요함
using System.Reflection.Emit; 

namespace Daqmx_test1
{
    public partial class Form1 : Form
    {
        bool Save = false;
        double voltageData0 = 0;
        Stopwatch _sw;
        bool Connect = false,reconnect=false;
        
        /******************************DATA******************************/
        double[,] VI_Data; //Ni 함수에 넣을 2차원 배열
        double[] VI_Save_Data = { 0, 0, 0, 0 }; //VI 용 실제 데이터 기록 변수
        double[] AO_Data= { 0,0,0,0 }; // 배열 4개구성에 대해 0으로 초기화 4개 설정 시 Analog 채널선택은 반드시 4개 여야 함
        /*****************************DAQMX****************************/
        NationalInstruments.DAQmx.Task Task1; // AI 태스크 기본 

        AnalogMultiChannelReader analogReader;
        NationalInstruments.DAQmx.Task Task2; // AO 태스크 기본 
        AnalogMultiChannelWriter analogWriter;

        /*****************************CallBack 함수 선언****************************/
        AsyncCallback analogCallback; //콜백함수(소환되는 함수)가 Ni Read 함수에 쓰임

        /******************************Save 관련 Thread 선언 *****************************/
        Thread saveThread;


        public Form1()
        {
            InitializeComponent();

            comboBox1.Items.AddRange(NationalInstruments.DAQmx.DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External)); //콤보박스에 AI 추가시키는 함수
            comboBox2.Items.AddRange(NationalInstruments.DAQmx.DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AO, PhysicalChannelAccess.External)); //콤보박스에 AO 추가시키는 함수
            if(comboBox1.Items.Count > 0 )comboBox1.SelectedIndex = 0; // 여러개가 요소로 있을 경우 0번의 값을 기본으로 지정
            if(comboBox2.Items.Count > 0 )comboBox2.SelectedIndex = 0; // 여러개가 요소로 있을 경우 0번의 값을 기본으로 지정
            toolStripLabel1.Text = "Save : Not Allocated";
            toolStripLabel1.ForeColor = Color.Red;
        }

        private void button1_Click(object sender, EventArgs e) // Connect 
        {
            try
            {

                if (Connect == false) // 처음은 버튼이 눌려있지 않음
                {
                    /******************************DAQMX*****************************/
                    Task1 = new NationalInstruments.DAQmx.Task(); // AI 태스크 기본 
                    Task2 = new NationalInstruments.DAQmx.Task(); // AO 태스크 기본 

                    //AI Configuration
                    Task1.AIChannels.CreateVoltageChannel(comboBox1.Text, "", AITerminalConfiguration.Differential, -10, 10, AIVoltageUnits.Volts);
                    Task1.Timing.ConfigureSampleClock("", 100, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, 100);

                    analogReader = new AnalogMultiChannelReader(Task1.Stream);
                    analogReader.SynchronizeCallbacks = true;

                    analogCallback = new AsyncCallback(callbackfunction_1);
                    analogReader.BeginReadMultiSample(100, analogCallback, Task1);

                    //AO Configuration
                    Task2.AOChannels.CreateVoltageChannel(comboBox2.Text, "", -10, 10, AOVoltageUnits.Volts);
                    analogWriter = new AnalogMultiChannelWriter(Task2.Stream);

                    /*******************처음 시작 시 Voltage Out 은  반드시 0 에서 시작*************************/
                    AO_Data[0] = Convert.ToDouble(textBox1.Text);
                    AO_Data[1] = Convert.ToDouble(textBox3.Text);
                    AO_Data[2] = Convert.ToDouble(textBox5.Text);
                    AO_Data[3] = Convert.ToDouble(textBox7.Text);

                    analogWriter.WriteSingleSample(true, AO_Data);
                    /*******************************************************************************************/


                    if (checkBox1.Checked != true)  // Save 체크박스가 해제된 채로 시작될 경우 방해를 받지 않도록 체크박스를 숨긴다. 

                    {
                        checkBox1.Visible = false;
                    }
                    else if (checkBox1.Checked == true) // Save 체크박스가 눌린 채로 시작될 경우 중복을 방지하기 위해 체크박스를 숨기고 저장 쓰레드를 시작한다. 
                    {
                        checkBox1.Visible = false;
                        saveThread = new Thread(() => saveThreadFunction());
                        saveThread.Start();
                    }
                    button1.Text = "연결해제"; // 버튼의 이름을 연결해제로 바꾼다. 
                    Connect = true;
                }

                else if (Connect == true) // 이미 버튼이 눌려 있을 경우 연결 해제 할 수 있도록 연결해제 버튼으로 변환
                {
                    if (saveThread != null)
                        saveThread.Abort();
                    Task1.Dispose();
                    Task2.Dispose();
                    button1.Text = "연결";
                    Connect=false;
                    reconnect = true;
                    checkBox1.Visible = true;
                    toolStripLabel1.Text = "Save : Not Allocated";
                    toolStripLabel1.ForeColor = Color.Red;
                    checkBox1.Checked = false;
                }
            }
            catch{MessageBox.Show("접속에 문제 발생..");}
            
        }
        void callbackfunction_1(IAsyncResult ar)
        {
            try {
                if (Connect == true)
                {
                    if ((Task1 != null) && (Task1 == ar.AsyncState))
                        VI_Data = analogReader.EndReadMultiSample(ar); // 출력이 전압 값으로 나오는 함수임
                    if (Task1 != null)
                        analogReader.BeginReadMultiSample(100, analogCallback, Task1); // CallBack 부르는 Delegate 함수 또 부름 

                    Invoke(new MethodInvoker(delegate () {

                        textBox2.Text = VI_Data[0, 0].ToString("f3"); VI_Save_Data[0] = VI_Data[0, 0];
                        textBox4.Text = VI_Data[1, 0].ToString("f3"); VI_Save_Data[1] = VI_Data[1, 0];
                        textBox6.Text = VI_Data[2, 0].ToString("f3"); VI_Save_Data[2] = VI_Data[2, 0];
                        textBox8.Text = VI_Data[3, 0].ToString("f3"); VI_Save_Data[3] = VI_Data[3, 0];
                    }));
                }
            }
            catch { MessageBox.Show("Callback 함수에 문제 발생.."); }
        }

        private void button2_Click(object sender, EventArgs e)  // 이 버튼을 누를 경우 Analog Out 값이 적용된다.
        {
            try
            {
                AO_Data[0] = Convert.ToDouble(textBox1.Text);
                AO_Data[1] = Convert.ToDouble(textBox3.Text);
                AO_Data[2] = Convert.ToDouble(textBox5.Text);
                AO_Data[3] = Convert.ToDouble(textBox7.Text);

                analogWriter.WriteSingleSample(true, AO_Data);
            }
            catch (Exception)
            {
                MessageBox.Show("값 입력 실패 .. ");
                throw;
            }
        }
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            
            if (checkBox1.Checked == true)
            {
                using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
                {
                    saveFileDialog1.Filter = "Dat_file.(*.dat)|*.dat";
                    saveFileDialog1.FilterIndex = 1;
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        toolStripLabel1.Text = saveFileDialog1.FileName;
                        toolStripLabel1.ForeColor= Color.Blue;
                    }
                    if (saveFileDialog1.FileName == "")
                    {
                        toolStripLabel1.Text = "Save : Not Allocated";
                        toolStripLabel1.ForeColor = Color.Red;
                        checkBox1.Checked = false;
                    }
                }
            }
            else
            {
                toolStripLabel1.Text = "Save : Not Allocated";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(saveThread != null)
            saveThread.Abort();
        }
        void saveThreadFunction()
        {
            try
            {
                _sw = new Stopwatch();
                    _sw.Start();

                    using (StreamWriter out_file = new StreamWriter(toolStripLabel1.Text))
                    {
                        out_file.WriteLine("Time\tData1\tData2\tData3\tData4");
                        do
                        {
                            out_file.WriteLine((Convert.ToDouble(_sw.ElapsedMilliseconds) / 1000).ToString("f3") + "\t" + VI_Save_Data[0].ToString("f3") + "\t" + VI_Save_Data[1].ToString("f3") + "\t" + VI_Save_Data[2].ToString("f3") + "\t" + VI_Save_Data[3].ToString("f3"));
                            Thread.Sleep(10);

                        } while (true);
                    }
            }
            catch (Exception) {  }
        }
    }
}
