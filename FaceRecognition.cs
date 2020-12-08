
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
using SmartCamera.Models;
using Emgu.CV.UI;
using System.Linq;
using SmartCamera.Repo;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace SmartCamera
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
        public List<LastDetected> lastDetecteds = new List<LastDetected>();
        public bool AppIsOnline { get; set; } = true;
        public FaceRecognition()
        {
            InitializeComponent();
        }

        public class CameraParameters {
            public Image<Bgr, Byte> currentFrame { get; set; }
            public Image<Bgr, byte> minimizedFrame { get; set; }

            public Capture grabber { get; set; }
            public Image<Gray, byte> result { get; set; } = null;
            public Image<Gray, byte> gray { get; set; } = null;
            public string CameraName { get; set; }
        }
        public class LastDetected
        {
            public string UserName { get; set; }
            public string DetectedAt { get; set; }
            public string CameraId { get; set; }
        }
        private void FaceRecognition_Load(object sender, EventArgs e)
        {
            if (!StaticMethods.CheckOnlineStatus()) {
                AppIsOnline = false;
                MessageBox.Show("Seems you are not online,data won't be sent to the cloud!","Application Offline",MessageBoxButtons.OK);
            }
            InitializeRecognizedFaces();
            var devices = new List<DsDevice>(DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice));
            var cameraNames = new List<string>();
            int camera = 1;
            //  StartCapturing(1);
            foreach (var device in devices)
            {
                if (camera != 2)
                {
                    cameraNames.Add(device.Name);
                    tableLayoutPanel1.Controls.Add(new ImageBox() { Name = device.Name.Replace(" ", "_"), Height = 460, Width = 638 }, 1, tableLayoutPanel1.RowCount - 1);
                    var cameraParameters = new CameraParameters();
                    cameraParameters.CameraName = device.Name;
                    cameraParameters.grabber = new Capture(camera - 1);
                    cameraParameters.grabber.QueryFrame();
                    Application.Idle += new EventHandler((s, ev) => FrameGrabber(s, ev, cameraParameters));
                }

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

        async void FrameGrabber(object sender, EventArgs e, CameraParameters cameraParameters)
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

            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                t = t + 1;
                cameraParameters.result = cameraParameters.currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                cameraParameters.minimizedFrame = cameraParameters.currentFrame.Copy(f.rect).Convert<Bgr, byte>().Resize(100, 100, INTER.CV_INTER_CUBIC);
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
                    CheckDetectedFacesAsync(facesDetected[0].Length, cameraParameters, name);
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
        private async System.Threading.Tasks.Task CheckDetectedFacesAsync(int facesDetected, CameraParameters cameraParameters, string name)
        {
            ///check if the user has been detected within the last minute 
            ///if detected ignore
            ///if not proceed to save the record
            ///this prevents oversaving the records. as detection is in microseconds.

            ///get the latest record for the username and camera.
            var lastRecord = lastDetecteds.Where(x => x.CameraId == cameraParameters.CameraName && x.UserName == name).OrderByDescending(x => x.DetectedAt).Take(1).SingleOrDefault();
            ///check the time validity 
            /// declare variable for the past one minute
            DateTime lastOneMinute = DateTime.Now.AddMinutes(-2);
            ///do the actual check
            if (lastRecord == null  ) //its either not detected or was detected very long ago
            {
                await FinalProcessing(cameraParameters, name);
            }
            else //active detection in place hence ignore 
            {

                if (Convert.ToDateTime(lastRecord.DetectedAt) > lastOneMinute)
                {

                }
                else
                {
                    //  Delete the users records in temp storage then re-add the user with new data and also post to the online portal.

                    //begin save locally
                    await FinalProcessing(cameraParameters, name);

                }
            }


            //if (facesDetected>0)
            //{

            /////  var currentImage=   cameraParameters.currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

            //    var fileName = Path.GetRandomFileName().Replace(".", "") + Path.GetRandomFileName().Replace(".","");
            //    var FolderName = "/DetectedFaces/" +DateTime.Now.Year+"/"+ DateTime.Now.Month+ "/" + DateTime.Now.Day + DateTime.Now.DayOfWeek+"/";
            //    var FinalPath = Application.StartupPath + FolderName;
            //    Directory.CreateDirectory(FinalPath);
            //    cameraParameters.minimizedFrame.Save(FinalPath + fileName + ".bmp");
            //    richTextBox1.AppendText(cameraParameters.CameraName + " -  "+ name + " -  " + facesDetected.ToString()+" faces  detected " + " at " + DateTime.Now.ToLongDateString()+"\n");
        }

        private async Task FinalProcessing(CameraParameters cameraParameters, string name)
        {
            var fileName = Path.GetRandomFileName().Replace(".", "") + Path.GetRandomFileName().Replace(".", "");
            var FolderName = "/DetectedFaces/" + DateTime.Now.Year + "/" + DateTime.Now.Month + "/" + DateTime.Now.Day + DateTime.Now.DayOfWeek + "/";
            var FinalPath = Application.StartupPath + FolderName;
            Directory.CreateDirectory(FinalPath);
            cameraParameters.minimizedFrame.Save(FinalPath + fileName + ".bmp");
            // End local  saving and begin sending data to portal if app is online
            if (AppIsOnline)
            {
                var newDetection = new LastDetected()
                {
                    CameraId = cameraParameters.CameraName,
                    DetectedAt = DateTime.Now.ToString(),
                    UserName = name
                };
                var serializedData = JsonConvert.SerializeObject(newDetection); // convert it to json
                var oldDetections = lastDetecteds.Where(x => x.CameraId == cameraParameters.CameraName && x.UserName == name).ToList();
                foreach (var item in oldDetections)
                {
                    lastDetecteds.Remove(item);
                }
                lastDetecteds.Add(newDetection);
                HttpClient client = new HttpClient();
                var Url = "https://localhost:44324/api/detectedfaces/PostImages";
                //  client.BaseAddress = new Uri("http://localhost:4354/api/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");
                var multipartContent = new MultipartFormDataContent();

                //var content = new StringContent(serializedData, Encoding.UTF8, "application/json");
                //multipartContent.Add(content);

                byte[] file = File.ReadAllBytes(FinalPath + fileName + ".bmp");
                Stream stream = new MemoryStream(file);
                using (var mem = new MemoryStream())
                {
                    await stream.CopyToAsync(mem);
                    var byteContent = new ByteArrayContent(mem.ToArray());
                    multipartContent.Add(byteContent, "detectedFace", fileName + ".bmp");
                    multipartContent.Add(new StringContent(newDetection.CameraId), "CameraId");
                    multipartContent.Add(new StringContent(newDetection.UserName), "UserName");
                    multipartContent.Add(new StringContent(newDetection.DetectedAt), "DetectedAt");

                    var response = await client.PostAsync(Url, multipartContent);
                }




            }
        }
    }

}
    

