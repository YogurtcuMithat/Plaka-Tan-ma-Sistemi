using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1;
using IronOcr;


namespace Plaka_Tanıma_Sistemi
{
    public partial class Form1 : Form
    {
        /* a) Kamera görüntüsü üzerinden plaka yerinin tespit edilmesi ve ayrıştırılması 
         * b) Plakanın sonraki algoritmalara uygun şekilde yeniden konumlandırılması ve boyutlandırılması 
         * c) Parlaklık, zıtlık gibi görüntü özelliklerinin normalizasyonu 
         * d) Karakter ayırma ile görüntüden karakterlerin çıkarılması 
         * e) Optik karakter tanıma 
         * f) Ülkeye özgü söz dizimi ve geometrik kontroller*/
        public Form1()
        {
            InitializeComponent();
        }
        public int[] sinir(List<IntPoint> list)
        {
            int[] sinirlar = { 0, 0, 0, 0 };
            int x1, x2, y1, y2;
            x1 = x2 = y1 = y2 = 0;
            bool first = true;
            foreach (AForge.IntPoint p in list)
            {
                if (first)
                {
                    x1 = x2 = p.X;
                    y1 = y2 = p.Y;
                    first = false;
                    continue;
                }
                if (p.X < x1) x1 = p.X;
                if (p.X > x2) x2 = p.X;
                if (p.Y < y1) y1 = p.Y;
                if (p.Y > y2) y2 = p.Y;
            }
            sinirlar[0] = x1;
            sinirlar[1] = x2;
            sinirlar[2] = y1;
            sinirlar[3] = y2;
            return sinirlar;
        }
        [System.Runtime.InteropServices.DllImport(@"OtsuEsikleme.dll")]//otsuEsikleme.dll'nin bulunduğu klasöre göre düzenlenmeli
        public static extern void OtsuEsikleme(ref byte pixelDizisi, ref byte esikDeger, int genislik, int yukseklik);

        private System.Drawing.Point[] ToPointsArray(List<IntPoint> points)
        {
            return points.Select(p => new System.Drawing.Point(p.X, p.Y)).ToArray();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Resim Dosyaları (*.bmp)|*.jpg;*.gif;*.bmp;*.png;*.jpeg";
            openFileDialog1.Multiselect = false;
            openFileDialog1.FileName = "";
            if (openFileDialog1.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            resimYukle(openFileDialog1.FileName);
            Bitmap bmp = new Bitmap(pictureBox1.Image);
            Bitmap bmpsobe;
            Bitmap bmpmedian;
            Bitmap otsu;
            if (pictureBox1.Image != null)
            {
                progressBar1.Visible = true;
                int i, j;
                Color ort;//Color sınıfından bir renk nesne tanımlıyoruz.

                //int r,g,b;
                progressBar1.Maximum = bmp.Width * bmp.Height;//İşlem çubuğunun maksimim olduğu yer for döngüsünün sonundaki piksel değerine erişmemiz durumundadır.
                for (i = 0; i <= bmp.Width - 1; i++)//dikey olarak görüntümüzü tarıyoruz.
                {
                    for (j = 0; j <= bmp.Height - 1; j++)//yatay olarak görüntümüzü tarıyoruz. 
                    {
                        ort = bmp.GetPixel(i, j);
                        ort = Color.FromArgb((byte)((ort.R + ort.G + ort.B) / 3), (byte)((ort.R + ort.G + ort.B) / 3), (byte)((ort.R + ort.G + ort.B) / 3));
                        bmp.SetPixel(i, j, ort);
                        if ((i % 10) == 0)//her on satırda bir göstergeyi güncelle
                        {
                            progressBar1.Value = i * bmp.Height + j;
                            Application.DoEvents();
                        }
                    }
                }
                //1)Uygulama sürecinde görüntü işleme hızını artırabilmek için RGB formattaki görüntü, ortalama değer yöntemiyle gri seviyeye indirgenmiştir. Byte veri tipi dönüşümü yapılır.
            }
            else MessageBox.Show("Önce resim seçilmeli");

            #region filtreler ve işlemler
            //////////////////////// Median //////////////
            //2 Gri seviyeye indirgenmiş olan görüntü üzerinde keskin geçişleri en az seviyeye indirmek için median filtre uygulanacaktır. 

            bmpmedian = ExtBitmap.MedianFilter(bmp, 3);

            /////////////////////// SOBEL ///////////////
            //Median filtre uygulanarak gürültüsü azaltılmış, keskin geçişler azaltılacaktır.Görüntü üzerinde kenar bulma işlemi için sobel filtresi kullanılacaktır.Dikey, yatay ve köşegen şeklindeki kenarları bulmak için kullanılacaktır.
            bmpsobe = ExtBitmap.Sobel3x3Filter(bmpmedian, true);
            Bitmap bmpsobe1 = (Bitmap)bmpsobe.Clone();

            /////////// otsu 3-Gri seviyeli görüntünün ikili seviyeye dönüştürülebilmesi için otsu algoritması kullanılacaktır.
            int x, y;
            int genislik = bmpsobe1.Width;
            int yukseklik = bmpsobe1.Height;
            byte[] pixeller = new byte[(int)genislik * yukseklik];
            Bitmap resim = (Bitmap)bmpsobe1.Clone();
            for (y = 0; y < yukseklik; y++)
                for (x = 0; x < genislik; x++)
                    // Pixelleri kütüphanenin işleyebileceği tek boyutlu bir diziye atıyoruz.
                    // Gri seviyede tüm ana renkler eşit olduğu için sadece kırmızıyı okumak gri seviye için yeterli.
                    pixeller[y * genislik + x] = resim.GetPixel(x, y).R;
            byte esikDeger = 0;
            OtsuEsikleme(ref pixeller[0], ref esikDeger, genislik, yukseklik);
            int renkk;
            for (y = 0; y < yukseklik; y++)
                for (x = 0; x < genislik; x++)
                {
                    renkk = pixeller[y * genislik + x]; // gri
                    resim.SetPixel(x, y, Color.FromArgb(renkk, renkk, renkk)); // Gri seviyeyi argb moduna dönüştürüp resme aktarıyoruz.
                }

            otsu = (Bitmap)resim.Clone();
            Bitmap bmperosion;
            Bitmap bmpdilation;
            Bitmap bmpclosing;

            //4- Görüntü üzerinde iskelet, imgedeki sınırlar gibi yapıların tanımlanması ve bilgi çıkarımı yapılması ve gürültü giderimi, bölütleme için matematiksel morfoloji işlemlerine ihtiyaç vardır. 
            //Bu işlem için görüntü üzerinde n boyutlu bir çekirdek gezdirilecektir. Ortadaki n. piksel, resim üzerinde işlem yaptığımız piksele karşılık gelir. 
            //Genişletme(dillatoin) işlemi aynı nesnenin bir gürültü ile ince bir şekilde bölünerek ayrı iki nesne gibi görünmesini engellemek için kullanılır.

            bmperosion = ExtBitmap.DilateAndErodeFilter(otsu, 3, WindowsFormsApp1.ExtBitmap.MorphologyType.Erosion, true, true, true);
            Bitmap bmperosionn = (Bitmap)bmperosion.Clone();
            bmpdilation = ExtBitmap.DilateAndErodeFilter(bmperosionn, 7, WindowsFormsApp1.ExtBitmap.MorphologyType.Dilation, true, true, true);
            Bitmap bmpdilationn = (Bitmap)bmpdilation.Clone();
            Bitmap one = (Bitmap)bmpdilationn.Clone();
            /*Aforge Kütüphanesi ve Damla Filtreleme İşlemi
            Aforge kütüphanesi görüntü işleme alanında kullanılan açık kaynak kodlu bir .NET kütüphanesidir.
            Görüntü üzerinde manuel olarak ya da otomatik olarak matematik işlemler yapılmasına olanak sağlar. 
            Filtrelemeden sinir ağları hesaplamalarına değin pek çok alanda kolaylıklar sağlar.*/
            BlobsFiltering filter0 = new BlobsFiltering();
            // Filtreyi yapılandırıyoruz
            filter0.CoupledSizeFiltering = true;
            filter0.MinWidth = 70;
            filter0.MinHeight = 40;
            // Filtreyi uyguluyoruz
            filter0.ApplyInPlace(one);


            Bitmap one2 = (Bitmap)one.Clone();
            bmpclosing = ExtBitmap.CloseMorphologyFilter(one2, 15, true, true, true);
            Bitmap bmperosio = (Bitmap)bmpclosing.Clone();

            Bitmap bmperosionson = ExtBitmap.DilateAndErodeFilter(bmperosio, 9, WindowsFormsApp1.ExtBitmap.MorphologyType.Erosion, true, true, true);
            Bitmap blobson = (Bitmap)bmperosionson.Clone();

            BlobsFiltering filterson = new BlobsFiltering();
            // Filtreyi yapılandırıyoruz
            filterson.CoupledSizeFiltering = true;
            filterson.MinWidth = 50;
            filterson.MinHeight = 200;
            // Filtreyi uyguluyoruz

            filterson.ApplyInPlace(blobson);
            Bitmap rect = (Bitmap)blobson.Clone();

            Bitmap one1 = (Bitmap)rect.Clone();

            ConnectedComponentsLabeling filter = new ConnectedComponentsLabeling();
            // Filtreyi uyguluyoruz
            Bitmap newImage = filter.Apply(one1);
            Bitmap newImage1 = (Bitmap)newImage.Clone();
            // nesne sayısını kontrol et
            int objectCount = filter.ObjectCount;

            BlobCounter blobCounter = new BlobCounter();
            blobCounter.ProcessImage(rect);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            // Grafik nesnesi oluşturuyoruz ( plaka alanını işaretlemek için)
            Graphics g = Graphics.FromImage(rect);
            Pen bluePen = new Pen(Color.Blue, 2);
            // her nesneyi kontrol edin ve nesnelerin etrafında bir daire çizin

            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                /*
                 * x1=0
                 * x2=1
                 * y1=2
                 * y2=3
                 */
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);
                List<IntPoint> corners = PointsCloud.FindQuadrilateralCorners(edgePoints);
                int[] sinirlar = sinir(corners);//plaka yerini bulduğumuz noktalar
                sinirlar[0] = sinirlar[0] - 2;
                sinirlar[1] = sinirlar[1] + 2;
                sinirlar[2] = sinirlar[2] - 2;
                sinirlar[3] = sinirlar[3] + 2;
                int en = sinirlar[1] - sinirlar[0];
                int boy = sinirlar[3] - sinirlar[2];
                float ort = (float)en / (float)boy;


                List<IntPoint> ucnoktalar = new List<IntPoint>();
                ucnoktalar.Add(new IntPoint(sinirlar[0], sinirlar[2]));
                ucnoktalar.Add(new IntPoint(sinirlar[1], sinirlar[2]));
                ucnoktalar.Add(new IntPoint(sinirlar[1], sinirlar[3]));
                ucnoktalar.Add(new IntPoint(sinirlar[0], sinirlar[3]));
                g.DrawPolygon(bluePen, ToPointsArray(ucnoktalar));
                g.DrawString("Plaka kordinatlari : (x,y): (" + sinirlar[0].ToString() + "," + sinirlar[2].ToString() + ")\n en, boy,ort: " + (sinirlar[1] - sinirlar[0]).ToString() + ", "
                 + (sinirlar[3] - sinirlar[2]).ToString() + "," + ort.ToString() + " blob sayisi:" + blobs.Length.ToString(), new Font("Arial", 8), Brushes.White, new System.Drawing.Point(sinirlar[0], sinirlar[3] + 4));

            }
            bluePen.Dispose();
            g.Dispose();

            Bitmap rect1 = (Bitmap)pictureBox1.Image.Clone();
            Graphics g1 = Graphics.FromImage(rect1);
            Pen bluePen2 = new Pen(Color.Red, 2);
            //her nesneyi kontrol edin ve nesnelerin etrafında bir daire çizin
            List<Blob> bloplar = new List<Blob>();
            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                /*              x1,y1--------x2,y1
                 * x1=0           |            |
                 * x2=1           |            |
                 * y1=2         x1,y2--------x2,y2
                 * y2=3
                 */
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);
                List<IntPoint> corners = PointsCloud.FindQuadrilateralCorners(edgePoints);
                int[] sinirlar = sinir(corners);
                sinirlar[0] = sinirlar[0] - 5;//plaka yerlerini boyutlarına göre ayarlıyoruz
                sinirlar[1] = sinirlar[1];
                sinirlar[2] = sinirlar[2] - 5;
                sinirlar[3] = sinirlar[3] + 5;
                int en = sinirlar[1] - sinirlar[0];
                int boy = sinirlar[3] - sinirlar[2];
                float ort = (float)en / (float)boy;
                if (ort >= 3 && ort <= 5.7)
                {

                    g1.DrawLines(bluePen2, new System.Drawing.Point[] { new System.Drawing.Point(sinirlar[0], sinirlar[2]),
                        new System.Drawing.Point(sinirlar[1] , sinirlar[2]), new System.Drawing.Point(sinirlar[1], sinirlar[3]),
                        new System.Drawing.Point(sinirlar[0], sinirlar[3]), new System.Drawing.Point( sinirlar[0], sinirlar[2]) });

                    g1.DrawString("Plaka kordinatlari : (x,y): (" + sinirlar[0].ToString() + "," + sinirlar[2].ToString() + ")\n en, boy,ort: " + (sinirlar[1] - sinirlar[0]).ToString() + ", "
                                + (sinirlar[3] - sinirlar[2]).ToString() + "," + ort.ToString() + " blob sayisi:" + blobs.Length.ToString(), new Font("Arial", 8), Brushes.White, new System.Drawing.Point(sinirlar[0], sinirlar[3] + 4));
                }
                else if (ort < 3)
                {
                    g1.DrawLines(bluePen2, new System.Drawing.Point[] { new System.Drawing.Point(sinirlar[0], sinirlar[2]),
                        new System.Drawing.Point(sinirlar[1] , sinirlar[2]), new System.Drawing.Point(sinirlar[1], sinirlar[3]),
                        new System.Drawing.Point(sinirlar[0], sinirlar[3]), new System.Drawing.Point( sinirlar[0], sinirlar[2]) });
                    //line çekiyoruz
                    g1.DrawString("Plaka kordinatlari : (x,y): (" + sinirlar[0].ToString() + "," + sinirlar[2].ToString() + ")\n en, boy,ort: " + (sinirlar[1] - sinirlar[0]).ToString() + ", "
                                + (sinirlar[3] - sinirlar[2]).ToString() + "," + ort.ToString() + " blob sayisi:" + blobs.Length.ToString(), new Font("Arial", 8), Brushes.White, new System.Drawing.Point(sinirlar[0], sinirlar[3] + 4));
                    //çektiğimiz çizginin satırını ekliyoruz
                }
                else if (ort > 5.7 && ort < 11)
                {
                    g1.DrawLines(bluePen2, new System.Drawing.Point[] { new System.Drawing.Point(sinirlar[0], sinirlar[2]),
                        new System.Drawing.Point(sinirlar[1] , sinirlar[2]), new System.Drawing.Point(sinirlar[1], sinirlar[3]),
                        new System.Drawing.Point(sinirlar[0], sinirlar[3]), new System.Drawing.Point( sinirlar[0], sinirlar[2]) });

                    g1.DrawString("Plaka kordinatlari : (x,y): (" + sinirlar[0].ToString() + "," + sinirlar[2].ToString() + ")\n en, boy,ort: " + (sinirlar[1] - sinirlar[0]).ToString() + ", "
                                + (sinirlar[3] - sinirlar[2]).ToString() + "," + ort.ToString() + " blob sayisi:" + blobs.Length.ToString(), new Font("Arial", 8), Brushes.White, new System.Drawing.Point(sinirlar[0], sinirlar[3] + 4));

                }
            }
            bluePen2.Dispose();
            g1.Dispose();
            #endregion

            Bitmap bn = null;
            Bitmap kes1 = (Bitmap)rect1.Clone();
            Graphics g2 = Graphics.FromImage(kes1);
            Pen bluePen3 = new Pen(Color.Red, 2);
            // her nesneyi kontrol edin ve nesnelerin etrafında bir daire çizin
            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                /*              x1,y1--------x2,y1
                 * x1=0           |            |
                 * x2=1           |            |
                 * y1=2         x1,y2--------x2,y2
                 * y2=3
                 */
                if (i == 0 && n > 1) continue;
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);
                List<IntPoint> corners = PointsCloud.FindQuadrilateralCorners(edgePoints);
                int[] sinirlar = sinir(corners);
                sinirlar[0] = sinirlar[0] - 5;
                sinirlar[1] = sinirlar[1];
                sinirlar[2] = sinirlar[2] - 5;
                sinirlar[3] = sinirlar[3] + 5;

                int en = sinirlar[1] - sinirlar[0];
                int boy = sinirlar[3] - sinirlar[2];
                float ort = en / boy;

                try
                {
                    if (ort >= 3 && ort <= 5.7)
                    {
                        g2.DrawLines(bluePen3, new System.Drawing.Point[] { new System.Drawing.Point(sinirlar[0], sinirlar[2]),
                        new System.Drawing.Point(sinirlar[1] , sinirlar[2]), new System.Drawing.Point(sinirlar[1], sinirlar[3]),
                        new System.Drawing.Point(sinirlar[0], sinirlar[3]), new System.Drawing.Point( sinirlar[0], sinirlar[2]) });

                        for (int w = 0; w < kes1.Width; w++)
                        {
                            for (int h = 0; h < kes1.Height; h++)
                            {
                                if ((w >= sinirlar[0] && w <= sinirlar[1]) && (h >= sinirlar[2]) && h <= sinirlar[3]) continue;
                                else kes1.SetPixel(w, h, Color.Black);  //çektiğimiz çizgiden kesiyoruz
                            }
                        }
                        bn = new Bitmap(en, boy);
                        Graphics g3 = Graphics.FromImage(bn);
                        g3.DrawImage(kes1, -sinirlar[0], -sinirlar[2]);
                        pictureBox2.Image = bn;//resmi koyuyoruz
                    }
                    else if (ort < 3)
                    {
                        g2.DrawLines(bluePen3, new System.Drawing.Point[] { new System.Drawing.Point(sinirlar[0], sinirlar[2]),
                        new System.Drawing.Point(sinirlar[1] , sinirlar[2]), new System.Drawing.Point(sinirlar[1], sinirlar[3]),
                        new System.Drawing.Point(sinirlar[0], sinirlar[3]), new System.Drawing.Point( sinirlar[0], sinirlar[2]) });

                        for (int w = 0; w < kes1.Width; w++)
                        {
                            for (int h = 0; h < kes1.Height; h++)
                            {
                                if ((w >= sinirlar[0] && w <= sinirlar[1]) && (h >= sinirlar[2]) && h <= sinirlar[3]) continue;
                                else kes1.SetPixel(w, h, Color.Black);
                            }
                            bn = new Bitmap(en, boy);
                            Graphics g3 = Graphics.FromImage(bn);
                            g3.DrawImage(kes1, -sinirlar[0], -sinirlar[2]);
                            pictureBox2.Image = bn;//resmi koyuyoruz
                        }
                    }
                    else
                    {
                        g2.DrawLines(bluePen3, new System.Drawing.Point[] { new System.Drawing.Point(sinirlar[0], sinirlar[2]),
                        new System.Drawing.Point(sinirlar[1] , sinirlar[2]), new System.Drawing.Point(sinirlar[1], sinirlar[3]),
                        new System.Drawing.Point(sinirlar[0], sinirlar[3]), new System.Drawing.Point( sinirlar[0], sinirlar[2]) });

                        for (int ii = 0; ii < kes1.Width; ii++)
                        {
                            for (int ji = 0; ji < kes1.Height; ji++)
                            {
                                if ((ii >= sinirlar[0] && ii <= sinirlar[1]) && (ji >= sinirlar[2]) && ji <= sinirlar[3]) continue;
                                else kes1.SetPixel(ii, ji, Color.Black);
                            }
                        }
                    }
                    bluePen3.Dispose();//bluePen3 kapatıyoruz
                    g2.Dispose();
                }
                catch (Exception)
                {
                    MessageBox.Show("Hay aksi! Çekirdeklerim eridi." );
                    break;
                }
                if (checkBox1.Checked == true)
                {
                    form(bmp, "Grayscala");
                    form(bmpmedian, "Median");
                    form(bmpsobe, "sobel");
                    form(otsu, "otsu");
                    form(bmperosion, "erosion");
                    form(bmpdilationn, "dilation");
                    form(one, "one");
                    form(bmpclosing, "closing");
                    form(bmperosionson, "son");
                    form(blobson, "blobson");
                    form(newImage, "one1");
                    form(rect, "rect");
                    form(rect1, "rect1");
                    form(kes1, "kes1");
                    form(bn, "Plaka");
                }
                else MessageBox.Show("Filtrelemeler görüntülenmeyecektir! \nFiltreleri görmek istiyorsanız: " + "\"Filtrelemer gösterilsin mi?\"" + "\nkutucuğunu işaretliyiniz.");
            }
        }
        public void form(Bitmap bmp, String isim)
        {
            Form2 form2 = new Form2();
            form2.Name = isim;
            form2.Text = isim;
            form2.setImage(bmp);//Form2.Designer.Cs
            if (bmp != null)
            {
                form2.Height = bmp.Height + 50;
                form2.Width = bmp.Width + 50;
            }
            form2.Show();
        }
        private void resimYukle(string resimYolu)
        {
            Bitmap resim = new Bitmap(resimYolu);

            if (resim.Width >= 480 || resim.Height >= 360)
            {
                float katsayi;
                int genislik;
                int yukseklik;
                // Resim boyutları pencereden taşacaksa, en/boy oranını koruyarak resmi yeniden boyutlandıralım
                if (resim.Width - 480 > resim.Height - 360)
                {
                    katsayi = (float)480 / resim.Width;
                    genislik = 480;
                    yukseklik = (int)(katsayi * resim.Height);
                }
                else
                {
                    katsayi = (float)360 / resim.Height;
                    yukseklik = 360;
                    genislik = (int)(katsayi * resim.Width);
                }
                Bitmap boyutlandirilmis = new Bitmap(genislik, yukseklik);
                Graphics grafik = Graphics.FromImage(boyutlandirilmis);
                grafik.DrawImage(resim, 0, 0, genislik, yukseklik);
                pictureBox1.Image = boyutlandirilmis;
            }
            else pictureBox1.Image = resim;
            pictureBox1.Enabled = false;
            Application.DoEvents();
        }
        #region ocr
        private void button2_Click(object sender, EventArgs e)
        {
            Stopwatch watch = Stopwatch.StartNew(); // zaman algılama süreci
            double time;
            var Ocr = new IronTesseract();
            Ocr.Configuration.TesseractVersion = TesseractVersion.Tesseract5;
            Ocr.Configuration.WhiteListCharacters = " QWERTYUIOPASDFGHJKLZXCVBNM0123456789";
            using (var Input = new OcrInput(pictureBox2.Image))
            {
                Input.Deskew();
                OcrResult Result = Ocr.Read(Input);
                textBox1.Text = Result.Text;
                if (textBox1.Text == "")
                {
                    watch.Stop(); MessageBox.Show("Gözlerimi kamaştırdın okuyamadım. \n Lütfen önümden çekil görebiliyim... ");
                }
            }
          
            time = watch.Elapsed.TotalSeconds;
            label1.Text = "Okuma işlemi " + time + " saniyede tamamlandı.";
            label1.Visible = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            /// Lisanıs için: https://ironsoftware.com/csharp/ocr/licensing/
            IronOcr.Installation.LicenseKey = "IRONOCR.HISTOHERTA.27374-62FF1487EC-TSHAMIKLKGV2PGZF-OO3OBCK76VHM-WMBDKUXQNAPR-UOTNXLVBUPXQ-VQYHQPUSS4XJ-LZRBXT-TMJ3ZBHR5FOAUA-DEPLOYMENT.TRIAL-G4SMQ5.TRIAL.EXPIRES.03.JUL.2022";
        }
        #endregion
    }
}

