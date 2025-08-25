using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CheckpointApp.Models;
using Xceed.Document.NET;
using Xceed.Words.NET; // <-- Используем библиотеку DocX

namespace CheckpointApp.Services
{
    public class WordExportService
    {
        // Метод выполняется асинхронно в фоновом потоке, чтобы не блокировать UI
        public Task ExportPersonReportAsync(
            Person person,
            IEnumerable<Vehicle> vehicles,
            IEnumerable<Crossing> crossings,
            IEnumerable<GoodReportItem> goodsSummary,
            string filePath)
        {
            return Task.Run(() =>
            {
                // Создаем новый Word-документ в памяти
                using (var document = DocX.Create(filePath))
                {
                    // --- Заголовок документа ---
                    document.InsertParagraph("ЗАПРОС НА ЛИЦО")
                        .Bold()
                        .FontSize(16)
                        .Alignment = Alignment.center;

                    document.InsertParagraph($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm:ss}")
                        .FontSize(10)
                        .Alignment = Alignment.center;

                    document.InsertParagraph(); // Пустая строка для отступа

                    // --- Раздел 1: Досье на лицо ---
                    document.InsertParagraph("1. Установочные данные")
                        .Bold()
                        .FontSize(14);

                    var dossierTable = document.AddTable(7, 2);
                    dossierTable.Design = TableDesign.TableGrid;
                    dossierTable.Alignment = Alignment.center;
                    dossierTable.AutoFit = AutoFit.Contents;

                    dossierTable.Rows[0].Cells[0].Paragraphs[0].Append("Фамилия:");
                    dossierTable.Rows[0].Cells[1].Paragraphs[0].Append(person.LastName);
                    dossierTable.Rows[1].Cells[0].Paragraphs[0].Append("Имя:");
                    dossierTable.Rows[1].Cells[1].Paragraphs[0].Append(person.FirstName);
                    dossierTable.Rows[2].Cells[0].Paragraphs[0].Append("Отчество:");
                    dossierTable.Rows[2].Cells[1].Paragraphs[0].Append(person.Patronymic ?? "-");
                    dossierTable.Rows[3].Cells[0].Paragraphs[0].Append("Дата рождения:");
                    dossierTable.Rows[3].Cells[1].Paragraphs[0].Append(person.Dob);
                    dossierTable.Rows[4].Cells[0].Paragraphs[0].Append("Гражданство:");
                    dossierTable.Rows[4].Cells[1].Paragraphs[0].Append(person.Citizenship);
                    dossierTable.Rows[5].Cells[0].Paragraphs[0].Append("Паспортные данные:");
                    dossierTable.Rows[5].Cells[1].Paragraphs[0].Append(person.PassportData);
                    dossierTable.Rows[6].Cells[0].Paragraphs[0].Append("Дополнительная информация:");
                    dossierTable.Rows[6].Cells[1].Paragraphs[0].Append(person.Notes ?? "-");

                    document.InsertTable(dossierTable);
                    document.InsertParagraph();

                    // --- Раздел 2: Транспортные средства ---
                    document.InsertParagraph("2. Использованные транспортные средства")
                        .Bold()
                        .FontSize(14);

                    var vehiclesTable = document.AddTable(vehicles.Count() + 1, 2);
                    vehiclesTable.Design = TableDesign.TableGrid;
                    vehiclesTable.Rows[0].Cells[0].Paragraphs[0].Append("Марка").Bold();
                    vehiclesTable.Rows[0].Cells[1].Paragraphs[0].Append("Гос. номер").Bold();

                    int v_row = 1;
                    foreach (var vehicle in vehicles)
                    {
                        vehiclesTable.Rows[v_row].Cells[0].Paragraphs[0].Append(vehicle.Make);
                        vehiclesTable.Rows[v_row].Cells[1].Paragraphs[0].Append(vehicle.LicensePlate);
                        v_row++;
                    }
                    document.InsertTable(vehiclesTable);
                    document.InsertParagraph();

                    // --- Раздел 3: История пересечений ---
                    document.InsertParagraph("3. История пересечений")
                        .Bold()
                        .FontSize(14);

                    var crossingsTable = document.AddTable(crossings.Count() + 1, 5);
                    crossingsTable.Design = TableDesign.TableGrid;
                    crossingsTable.Rows[0].Cells[0].Paragraphs[0].Append("Дата и время").Bold();
                    crossingsTable.Rows[0].Cells[1].Paragraphs[0].Append("Направление").Bold();
                    crossingsTable.Rows[0].Cells[2].Paragraphs[0].Append("Тип").Bold();
                    crossingsTable.Rows[0].Cells[3].Paragraphs[0].Append("Цель").Bold();
                    crossingsTable.Rows[0].Cells[4].Paragraphs[0].Append("НП Следования").Bold();

                    int c_row = 1;
                    foreach (var crossing in crossings)
                    {
                        crossingsTable.Rows[c_row].Cells[0].Paragraphs[0].Append(crossing.Timestamp);
                        crossingsTable.Rows[c_row].Cells[1].Paragraphs[0].Append(crossing.Direction);
                        crossingsTable.Rows[c_row].Cells[2].Paragraphs[0].Append(crossing.CrossingType);
                        crossingsTable.Rows[c_row].Cells[3].Paragraphs[0].Append(crossing.Purpose ?? "-");
                        crossingsTable.Rows[c_row].Cells[4].Paragraphs[0].Append(crossing.DestinationTown ?? "-");
                        c_row++;
                    }
                    document.InsertTable(crossingsTable);
                    document.InsertParagraph();

                    // --- Раздел 4: Сводка по товарам ---
                    document.InsertParagraph("4. Сводка по перемещенным товарам и грузам")
                        .Bold()
                        .FontSize(14);

                    var goodsTable = document.AddTable(goodsSummary.Count() + 1, 3);
                    goodsTable.Design = TableDesign.TableGrid;
                    goodsTable.Rows[0].Cells[0].Paragraphs[0].Append("Наименование").Bold();
                    goodsTable.Rows[0].Cells[1].Paragraphs[0].Append("Общее количество").Bold();
                    goodsTable.Rows[0].Cells[2].Paragraphs[0].Append("Ед. изм.").Bold();

                    int g_row = 1;
                    foreach (var good in goodsSummary)
                    {
                        goodsTable.Rows[g_row].Cells[0].Paragraphs[0].Append(good.Description);
                        goodsTable.Rows[g_row].Cells[1].Paragraphs[0].Append(good.TotalQuantity.ToString());
                        goodsTable.Rows[g_row].Cells[2].Paragraphs[0].Append(good.Unit);
                        g_row++;
                    }
                    document.InsertTable(goodsTable);

                    // Сохраняем документ на диск
                    document.Save();
                }
            });
        }
    }
}
