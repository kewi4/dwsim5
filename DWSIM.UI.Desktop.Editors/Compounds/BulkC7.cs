﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.BaseClasses;
using Eto.Drawing;
using Eto.Forms;
using s = DWSIM.UI.Shared.Common;

using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Desktop.Shared;

using DWSIM.ExtensionMethods;
using System.IO;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Desktop.Shared.Controls;
using DWSIM.Thermodynamics.Utilities.PetroleumCharacterization;

namespace DWSIM.UI.Desktop.Editors
{
    public class BulkC7PCharacterization : DynamicLayout
    {

        public IFlowsheet flowsheet;

        private String assayname;

        int ncomps = 10;
        double t1, t2, v1, v2, mw0, sg0, nbp0;
        SampleType type = SampleType.Light;
        Nullable<double> mw, sg, nbp;


        public BulkC7PCharacterization(IFlowsheet fs): base()
        {
            flowsheet = fs;
            Init();
        }

        void Init()
        {

            Padding = new Padding(10);

            mw0 = 80.0f;
            sg0 = 0.70f;
            nbp0 = 333.0f;

            t1 = 38 + 273.15;
            t2 = 98.9 + 273.15;

            assayname = "OIL";
            v1 = 0;
            v2 = 0;

            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            s.CreateAndAddLabelRow(this, "Assay Identification");
            s.CreateAndAddStringEditorRow(this, "Assay Name", assayname, (arg3, arg2) =>
            {
                assayname = arg3.Text;
            });
            s.CreateAndAddDescriptionRow(this, "Enter the name of the assay. It will be used to identify the Material Stream on the flowsheet and the associated compounds as well.");

            s.CreateAndAddLabelRow(this, "Assay Properties");
            s.CreateAndAddDescriptionRow(this, "Select the assay type and define at least one of the following three properties in order to calculate a property distribution.");
            s.CreateAndAddDropDownRow(this, "Assay Type", new List<string>(){"Light", "Average", "Heavy"}, 0, (arg3, arg2) =>
            {
                switch (arg3.SelectedIndex)
                {
                    case 0:
                        type = SampleType.Light;
                        break;
                    case 1:
                        type = SampleType.Average;
                        break;
                    case 2:
                        type = SampleType.Heavy;
                        break;
                }
            });
            s.CreateAndAddDescriptionRow(this, "Select the type of the assay. Property calculation methods will be selected according to this setting.");
            s.CreateAndAddTextBoxRow(this, nf, "Molar Weight", mw.GetValueOrDefault(), (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    mw = Double.Parse(arg3.Text);
                }
            });
            s.CreateAndAddDescriptionRow(this, "Leave it unchanged if not available.");
            s.CreateAndAddTextBoxRow(this, nf, "Specific Gravity", sg.GetValueOrDefault(), (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    sg = Double.Parse(arg3.Text);
                }
            });
            s.CreateAndAddDescriptionRow(this, "Leave it unchanged if not available.");
            s.CreateAndAddTextBoxRow(this, nf, "Average NBP (" + su.temperature + ")", cv.ConvertFromSI(su.temperature, nbp.GetValueOrDefault()), (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    nbp = cv.ConvertToSI(su.temperature, Double.Parse(arg3.Text));
                }
            });
            s.CreateAndAddDescriptionRow(this, "Leave it unchanged if not available.");


            s.CreateAndAddLabelRow(this, "Initial Values for Property Distribution");
            s.CreateAndAddTextBoxRow(this, nf, "Molar Weight", mw0, (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    mw0 = Double.Parse(arg3.Text);
                }
            });
            s.CreateAndAddDescriptionRow(this, "This defines the Molar Weight of the lightest compound in the assay.");
            s.CreateAndAddTextBoxRow(this, nf, "Specific Gravity", sg0, (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    sg0 = Double.Parse(arg3.Text);
                }
            });
            s.CreateAndAddDescriptionRow(this, "This defines the Specific Gravity of the lightest compound in the assay.");
            s.CreateAndAddTextBoxRow(this, nf, "Normal Boiling Point (" + su.temperature + ")", cv.ConvertFromSI(su.temperature, nbp0), (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    nbp0 = cv.ConvertToSI(su.temperature, Double.Parse(arg3.Text));
                }
            });
            s.CreateAndAddDescriptionRow(this, "This defines the Normal Boiling Point of the lightest compound in the assay.");

            s.CreateAndAddLabelRow(this, "Pseudo Compounds");
            s.CreateAndAddTextBoxRow(this, "N0", "Number of Compounds", ncomps, (arg3, arg2) =>
            {
                if (s.IsValidDouble(arg3.Text))
                {
                    ncomps = int.Parse(arg3.Text);
                }
            });
            s.CreateAndAddDescriptionRow(this, "Specify the number of compounds to be generated that, together, will represent the assay. The generated compounds will be added to the simulation and a Material Stream will be created with distribution-defined amounts of these compounds.");

            s.CreateAndAddButtonRow(this, "Characterize Assay and Create Compounds", null, (arg3, arg2) =>
            {

                var dialog = ProgressDialog.Show(this, "Petroleum C7+ Characterization", "Generating compounds, please wait...", false);

                var comps = new Dictionary<string,  ICompound>();

                Task.Factory.StartNew(() =>
                {
                    comps = new GenerateCompounds().GenerateCompounds(assayname, ncomps, type, mw, sg, nbp, v1, v2, t1, t2, mw0, sg0, nbp0);
                    foreach (var comp in comps.Values)
                    {
                        if (!flowsheet.AvailableCompounds.ContainsKey(comp.Name))
                        {
                            flowsheet.AvailableCompounds.Add(comp.Name, comp.ConstantProperties);
                        }
                        flowsheet.SelectedCompounds.Add(comp.Name, flowsheet.AvailableCompounds[comp.Name]);
                        foreach (MaterialStream obj in flowsheet.SimulationObjects.Values.Where((x) => x.GraphicObject.ObjectType == ObjectType.MaterialStream))
                        {
                            foreach (var phase in obj.Phases.Values)
                            {
                                phase.Compounds.Add(comp.Name, new Thermodynamics.BaseClasses.Compound(comp.Name, ""));
                                phase.Compounds[comp.Name].ConstantProperties = flowsheet.SelectedCompounds[comp.Name];
                            }
                        }
                    }
                    var ms = (MaterialStream)flowsheet.AddObject(ObjectType.MaterialStream, 100, 100, assayname);
                    double wtotal = comps.Values.Select((x) => x.MoleFraction.GetValueOrDefault() * x.ConstantProperties.Molar_Weight).Sum();
                    foreach (var c in ms.Phases[0].Compounds.Values)
                    {
                        c.MassFraction = 0.0f;
                        c.MoleFraction = 0.0f;
                    }
                    foreach (var c in comps.Values)
                    {
                        c.MassFraction = c.MoleFraction.GetValueOrDefault() * c.ConstantProperties.Molar_Weight / wtotal;
                        ms.Phases[0].Compounds[c.Name].MassFraction = c.MassFraction.GetValueOrDefault();
                        ms.Phases[0].Compounds[c.Name].MoleFraction = c.MoleFraction.GetValueOrDefault();
                    }
                }).ContinueWith((t) =>
                {
                    Application.Instance.Invoke(() => { dialog.Close(); });
                    if (t.Exception == null)
                    {
                        Application.Instance.Invoke(() =>
                        {

                            flowsheet.UpdateInterface();
                            flowsheet.ShowMessage("Material Stream '" + assayname + "' added successfully. " + ncomps.ToString() + " compounds created.", IFlowsheet.MessageType.Information);

                            if (MessageBox.Show("Do you want to export the created compounds to a XML database?", "Petroleum C7+ Characterization", MessageBoxButtons.YesNo, MessageBoxType.Question, MessageBoxDefaultButton.Yes) == DialogResult.Yes)
                            {
                                try
                                {
                                    var compstoexport = comps.Values.Select((x) => x.ConstantProperties).ToArray();
                                    var savedialog = new SaveFileDialog();
                                    savedialog.Title = "Save Compounds to XML Database";
                                    savedialog.Filters.Add(new FileFilter("XML File", new[] { ".xml" }));
                                    savedialog.CurrentFilterIndex = 0;
                                    if (savedialog.ShowDialog(this) == DialogResult.Ok)
                                    {
                                        try
                                        {
                                            if (!File.Exists(savedialog.FileName))
                                            {
                                                File.WriteAllText(savedialog.FileName, "");
                                                Thermodynamics.Databases.UserDB.CreateNew(savedialog.FileName, "compounds");
                                            }
                                            Thermodynamics.Databases.UserDB.AddCompounds(compstoexport, savedialog.FileName, true);
                                            flowsheet.ShowMessage("Compounds successfully saved to XML file.", IFlowsheet.MessageType.Information);
                                        }
                                        catch (Exception ex)
                                        {
                                            flowsheet.ShowMessage("Error saving compound to JSON file: " + ex.ToString(), IFlowsheet.MessageType.GeneralError);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    flowsheet.ShowMessage("Error saving data: " + ex.ToString(), IFlowsheet.MessageType.GeneralError);
                                }
                            }
                        });
                    }
                    else
                    {
                        Application.Instance.Invoke(() =>
                        {
                            flowsheet.ShowMessage("Error saving data: " + t.Exception.GetBaseException().Message, IFlowsheet.MessageType.GeneralError);
                        });
                    }
                });
            });
        }
    }
}
