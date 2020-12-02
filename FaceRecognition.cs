
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Diagnostics;
using System.Management;
using DirectShowLib;
using MultiFaceRec.Models;
using Emgu.CV.UI;

namespace MultiFaceRec
{
    public partial class FaceRecognition : Form
    {




        HaarCascade face = new HaarCascade("haarcascade_frontalface_default.xml");
        HaarCascade eye;
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        List<string> NamePersons = new List<string>();
        int ContTrain, NumLabels, t;
        string name, names = null;
        public static event EventHandler Idle;
        public FaceRecognition()
        {
                InitializeComponent();
        }

        public class CameraParameters{
          public  Image<Bgr, Byte> currentFrame { get; set; }
            public Capture grabber { get; set; }
            public Image<Gray, byte> result { get; set; } = null;
            public Image<Gray, byte> gray { get; set; } = null;
            public string CameraName { get; set; }
        }
        private void FaceRecognition_Load(object sender, EventArgs e)
        {
            InitializeRecognizedFaces();
            var devices = new List<DsDevice>(DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice));
            var cameraNames = new List<string>();
            int camera = 1;
            //  StartCapturing(1);
            foreach (var device in devices)
            {
                cameraNames.Add(device.Name);
                tableLayoutPanel1.Controls.Add(new ImageBox() { Name = device.Name.Replace(" ","_"), Height = 460, Width = 638 }, 1, tableLayoutPanel1.RowCount - 1);
                var cameraParameters = new CameraParameters();
                cameraParameters.CameraName = device.Name;
                cameraParameters.grabber = new Capture(camera - 1);
                cameraParameters.grabber.QueryFrame();
                Application.Idle += new EventHandler((s, ev) => FrameGrabber(s, ev, cameraParameters));
                camera++;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FaceTraining form = new FaceTraining();
           // form.Show();

            this.Hide();
            form.ShowDialog();
            this.Close();
        }

        async void FrameGrabber(object sender, EventArgs e,CameraParameters cameraParameters)
        {
            GetFrame1Data(cameraParameters);
        }
        private async void GetFrame1Data(CameraParameters cameraParameters)
        {
            NamePersons.Add("");

            //Get the current frame form capture device
            cameraParameters.currentFrame = cameraParameters.grabber.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
            //Convert it to Grayscale
            cameraParameters.gray = cameraParameters.currentFrame.Convert<Gray, Byte>();

            //Face Detector
            MCvAvgComp[][] facesDetected = cameraParameters.gray.DetectHaarCascade(
          face,
          1.2,
          10,
          Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
          new Size(20, 20));
            CheckDetectedFaces(facesDetected[0].Length,cameraParameters.CameraName);
            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                t = t + 1;
                cameraParameters.result = cameraParameters.currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //draw the face detected in the 0th (gray) channel with blue color
                cameraParameters.currentFrame.Draw(f.rect, new Bgr(Color.Red), 2);
                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                    //Eigen face recognizer
                    EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                       trainingImages.ToArray(),
                       labels.ToArray(),
                       3000,
                       ref termCrit);
                    name = recognizer.Recognize(cameraParameters.result);

                    //Draw the label for each face detected and recognized
                    cameraParameters.currentFrame.Draw(name, ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));
                }

                NamePersons[t - 1] = name;
                NamePersons.Add("");
            }
            t = 0;

            //Names concatenation of persons recognized
            for (int nnn = 0; nnn < facesDetected[0].Length; nnn++)
            {
                names = names + NamePersons[nnn] + ", ";
            }
            //Show the faces procesed and recognized

            ImageBox img = Controls.Find(cameraParameters.CameraName.Replace(" ", "_"), true)[0] as ImageBox;
            img.Image = cameraParameters.currentFrame;
            // label4.Text = names;
            if (names != "" && names != null)
                // richTextBox1.AppendText(names + " at " + DateTime.Now.ToLongDateString());
                names = "";
            //Clear the list(vector) of names
            NamePersons.Clear();
        }

        private void InitializeRecognizedFaces()
        {
            string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
            string[] Labels = Labelsinfo.Split('%');
            NumLabels = Convert.ToInt16(Labels[0]);
            ContTrain = NumLabels;
            string LoadFaces;

            for (int tf = 1; tf < NumLabels + 1; tf++)
            {
                LoadFaces = "face" + tf + ".bmp";
                trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                labels.Add(Labels[tf]);
            }
        }
        private void CheckDetectedFaces(int facesDetected,string cameraNo)
        {
            if (facesDetected>0)
            {
                richTextBox1.AppendText(cameraNo +" - "+ facesDetected.ToString()+" faces  detected " + " at " + DateTime.Now.ToLongDateString()+"\n");
            }
        }

      
    }
}
