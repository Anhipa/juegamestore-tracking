﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;

namespace JugamestoreTracking
{
    public partial class Form1 : Form
    {
        #region Atributos
        DataTable dtJuegos;
        bool detenerProceso;
        #endregion

        #region Constructor
        public Form1()
        {
            InitializeComponent();
        }
        #endregion

        #region Eventos
        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = this.Text + " " + Application.ProductVersion;

            try
            {
                if (File.Exists(Properties.Settings.Default.FicheroCargaInicial))
                {
                    this.cargarFicheroJuegos(Properties.Settings.Default.FicheroCargaInicial);
                }
                else
                {
                    this.inicializarTablaJuegos();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private void salirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private void importarJuegosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                this.btnDetener.Visible = true;
                this.importarJuegos();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                this.btnDetener.Visible = false;
                this.tsslStatus.Text = "Inactivo";
                this.tsslDetalle.Text = "-";
            }
        }
        private void guardarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(Properties.Settings.Default.FicheroCargaInicial) &&
                    (this.sfdListaJuegos.ShowDialog() != System.Windows.Forms.DialogResult.OK))
                {
                    return;
                }

                // Almacenar el fichero creado
                ((DataTable)this.dataGridView1.DataSource).WriteXml(this.sfdListaJuegos.FileName);
                ((DataTable)this.dataGridView1.DataSource).WriteXmlSchema(this.sfdListaJuegos.FileName.Replace("xml", "xsd"));

                // Almacenamos el nombre del fichero para la proxima apertura
                Properties.Settings.Default.FicheroCargaInicial = this.ofdListaJuegos.FileName;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());

            }
        }
        private void abrirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.ofdListaJuegos.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.cargarFicheroJuegos(this.ofdListaJuegos.FileName);

                    // Almacenamos el nombre del fichero para la proxima apertura
                    Properties.Settings.Default.FicheroCargaInicial = this.ofdListaJuegos.FileName;
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private void btnDetener_Click(object sender, EventArgs e)
        {
            this.detenerProceso = true;
        }
        private void nuevoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.inicializarTablaJuegos();
        }
        #endregion

        #region Metodos privados
        private void importarJuegos()
        {
            this.tsslStatus.Text = "Consultando...";
            this.tsslDetalle.Text = "Pagina principal";
            this.detenerProceso = false;
            Application.DoEvents();

            Uri webJuegame = new Uri("http://www.juegamestore.es/index.php?route=product/category&path=51&page=1&limit=500");
            WebRequest consulta = WebRequest.Create(webJuegame);
            WebResponse respuesta = consulta.GetResponse();
            StreamReader reader = new StreamReader(respuesta.GetResponseStream());
            Regex rx = new Regex("product_id=(\\d*)");
            MatchCollection ss = rx.Matches(reader.ReadToEnd());
            List<int> productos = new List<int>();

            foreach (Match match in ss)
            {
                int producto;
                if (int.TryParse(match.Groups[1].Value.ToString(), out producto))
                {
                    if (!productos.Contains(producto)) productos.Add(producto);
                }
            }

            var juegosActivos = from juegos in this.dtJuegos.AsEnumerable()
                                where juegos.Field<DateTime>("FechaBaja") == DateTime.Parse("01/01/1990")
                                select juegos;
            if (productos.Count != juegosActivos.Count())
            {
                int inicio = 1;
                foreach (int item in productos)
                {
                    if (detenerProceso) break;

                    this.tsslContar.Text = inicio++ + "\\" + productos.Count;
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);

                    Uri webJuego = new Uri(string.Format("http://www.juegamestore.es/index.php?route=product/product&product_id={0}", item));
                    WebRequest consultaJuego = WebRequest.Create(webJuego);
                    WebResponse respuestaJuego = consultaJuego.GetResponse();
                    StreamReader readerJuego = new StreamReader(respuestaJuego.GetResponseStream());
                    string htmlJuego = readerJuego.ReadToEnd();

                    this.tsslDetalle.Text = "Producto de juegamestore " + webJuego.AbsoluteUri;
                    Application.DoEvents();

                    Regex rxTitulo = new Regex("<h1>(.*?)<\\/h1>");
                    Regex rxPrecioOld = new Regex("price-old\">(\\d*.\\d*)");
                    Regex rxPrecioNew = new Regex("price-new\">(\\d*.\\d*)");
                    Regex rxEstado = new Regex("Estado:<\\/span>(.*?)<");
                    Regex rxDisponibilidad = new Regex("Disponibilidad:<\\/span>(.*?)<");
                    Regex rxFabricante = new Regex("Fabricante:<\\/span>.*?<a.*?>(.*?)<");
                    Regex rxPaginaBGGv1 = new Regex("(http.*?:\\/\\/www.boardgamegeek.com.*?)\"");
                    Regex rxPaginaBGGv2 = new Regex("(http.*?:\\/\\/boardgamegeek.com.*?)\"");

                    Regex rxPuntuacionBGG = new Regex("\"average\":\"(.*?)\"");

                    string titulo = rxTitulo.Match(htmlJuego).Groups[1].Value.ToString();

                    string precioOld = rxPrecioOld.Match(htmlJuego).Groups[1].Value.ToString().Replace(".", ",");
                    decimal precioOldDecimal;
                    decimal.TryParse(precioOld, out precioOldDecimal);
                    precioOldDecimal = decimal.Round(precioOldDecimal, 2);

                    string precioNew = rxPrecioNew.Match(htmlJuego).Groups[1].Value.ToString().Replace(".", ",");
                    decimal precioNewDecimal;
                    decimal.TryParse(precioNew, out precioNewDecimal);
                    precioNewDecimal = decimal.Round(precioNewDecimal, 2);

                    string estado = rxEstado.Match(htmlJuego).Groups[1].Value.ToString();

                    string disponibilidad = rxDisponibilidad.Match(htmlJuego).Groups[1].Value.ToString();
                    int disponibilidadNumero;
                    Int32.TryParse(disponibilidad, out disponibilidadNumero);

                    string fabricante = rxFabricante.Match(htmlJuego).Groups[1].Value.ToString();
                    string paginaBGGv1 = rxPaginaBGGv1.Match(htmlJuego).Groups[1].Value.ToString();
                    string paginaBGGv2 = rxPaginaBGGv2.Match(htmlJuego).Groups[1].Value.ToString();
                    string paginaBGG = !string.IsNullOrEmpty(paginaBGGv1) ? paginaBGGv1 : paginaBGGv2;

                    decimal puntuacionBGGDecimal = 0;

                    // Consulta puntuacion a la pagina de la BGG
                    string puntuacionBGG = string.Empty;
                    if (!string.IsNullOrEmpty(paginaBGG) &&
                        !this.dtJuegos.Rows.Contains(new object[] { item }))
                    {
                        this.tsslDetalle.Text = "Producto de boardgamegeek " + paginaBGG;
                        Application.DoEvents();

                        Uri webJuegoBGG = new Uri(paginaBGG.Replace("/images", "")); ;
                        WebRequest consultaJuegoBGG = WebRequest.Create(webJuegoBGG);
                        WebResponse respuestaJuegoBGG = consultaJuegoBGG.GetResponse();
                        StreamReader readerJuegoBGG = new StreamReader(respuestaJuegoBGG.GetResponseStream());
                        string htmlJuegoBGG = readerJuegoBGG.ReadToEnd();

                        puntuacionBGG = rxPuntuacionBGG.Match(htmlJuegoBGG).Groups[1].Value.ToString().Replace(".", ",");
                        decimal.TryParse(puntuacionBGG, out puntuacionBGGDecimal);
                    }

                    decimal descueltoDecimal = 0;
                    if (precioOldDecimal > 0) descueltoDecimal = (100 - ((100 * precioNewDecimal) / precioOldDecimal));
                    descueltoDecimal = decimal.Round(descueltoDecimal, 2);

                    if (this.dtJuegos.Rows.Contains(new object[] { item }))
                    {
                        DataRow juego = this.dtJuegos.Rows.Find(new object[] { item });
                        if (Convert.ToDecimal(juego["PrecioRebaja"]) != precioNewDecimal)
                        {
                            juego["FechaActualizacion"] = DateTime.Now.ToString("dd/MM/yyyy");
                            juego["PrecioRebaja"] = precioNewDecimal;
                            juego["Descuento"] = descueltoDecimal;
                            juego["Notas"] = juego["Notas"] + "Precio (" + juego["PrecioRebaja"] + "->" + precioNewDecimal + ") | ";
                        }
                        if (Convert.ToDecimal(juego["Disponibilidad"]) != disponibilidadNumero)
                        {
                            juego["FechaActualizacion"] = DateTime.Now.ToString("dd/MM/yyyy");
                            juego["Disponibilidad"] = disponibilidadNumero;
                            juego["Notas"] = juego["Notas"] + "Disponibilidad (" + juego["Disponibilidad"] + "->" + disponibilidadNumero + ") | ";
                        }
                    }
                    else
                    {
                        this.dtJuegos.Rows.Add(new object[] {
                    item,
                    fabricante,
                    titulo,
                    precioOldDecimal,  
                    precioNewDecimal, 
                    descueltoDecimal, 
                    estado.Trim(),
                    disponibilidadNumero,
                    decimal.Round(puntuacionBGGDecimal, 2), 
                    webJuego.AbsoluteUri,
                    DateTime.Now.ToString("dd/MM/yyyy"),
                    DateTime.Parse("01/01/1990"),
                    DateTime.Parse("01/01/1990"),
                    string.Empty
                    });
                    }

                    this.dataGridView1.DataSource = this.dtJuegos;

                    Application.DoEvents();
                }

                // Dar de baja los no encontrados
                foreach (DataRow juegoItem in this.dtJuegos.Rows)
                {
                    if (!productos.Contains(Convert.ToInt32(juegoItem["item"])))
                    {
                        juegoItem["FechaBaja"] = DateTime.Now.ToString("dd/MM/yyyy");
                    }
                }
            }
            else
            {
                MessageBox.Show("No se han detectado cambios", "AVISO", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            }

            this.dataGridView1.DataSource = this.dtJuegos;

            Application.DoEvents();
        }
        private void inicializarTablaJuegos()
        {
            this.dtJuegos = new DataTable("JuegosJuegamestore");
            dtJuegos.Columns.Add("Item");
            dtJuegos.Columns.Add("Fabricante");
            dtJuegos.Columns.Add("Nombre");
            dtJuegos.Columns.Add("PrecioAntes", typeof(Decimal));
            dtJuegos.Columns.Add("PrecioRebaja", typeof(Decimal));
            dtJuegos.Columns.Add("Descuento", typeof(Decimal));
            dtJuegos.Columns.Add("Estado");
            dtJuegos.Columns.Add("Disponibilidad", typeof(Int32));
            dtJuegos.Columns.Add("PuntuacionBGG", typeof(Decimal));
            dtJuegos.Columns.Add("URL");
            dtJuegos.Columns.Add("FechaAlta", typeof(DateTime));
            dtJuegos.Columns.Add("FechaBaja", typeof(DateTime));
            dtJuegos.Columns.Add("FechaActualizacion", typeof(DateTime));
            dtJuegos.Columns.Add("Notas");

            dtJuegos.PrimaryKey = new DataColumn[] { dtJuegos.Columns["Item"] };
            this.dataGridView1.DataSource = dtJuegos;
        }

        private void cargarFicheroJuegos(string fichero)
        {
            DataSet juegosCargados = new DataSet();
            juegosCargados.ReadXmlSchema(fichero.Replace("xml", "xsd"));
            juegosCargados.ReadXml(fichero);

            this.dtJuegos = juegosCargados.Tables["JuegosJuegamestore"];
            this.dataGridView1.DataSource = this.dtJuegos;
        }
        #endregion

        private void guardarComoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.sfdListaJuegos.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Almacenar el fichero creado
                    ((DataTable)this.dataGridView1.DataSource).WriteXml(this.sfdListaJuegos.FileName);
                    ((DataTable)this.dataGridView1.DataSource).WriteXmlSchema(this.sfdListaJuegos.FileName.Replace("xml", "xsd"));

                    // Almacenamos el nombre del fichero para la proxima apertura
                    Properties.Settings.Default.FicheroCargaInicial = this.sfdListaJuegos.FileName;
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());

            }
        }

    }
}