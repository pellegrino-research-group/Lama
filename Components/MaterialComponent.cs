using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Alpaca4d.UIWidgets;
using Lama.Materials;

namespace Lama.Components
{
    public class MaterialComponent : GH_SwitcherComponent
    {
        public MaterialComponent()
            : base(
                "Material",
                "Mat",
                "Define material properties for structural analysis",
                "Lama",
                "Materials")
        {
        }

        protected override string DefaultEvaluationUnit => "Isotropic";

        public override string UnitMenuName => "Material Type";

        public override string UnitMenuHeader => "Select material type";

        // Required by GH_Component but not used in GH_SwitcherComponent
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Parameters are registered via EvaluationUnits
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Parameters are registered via EvaluationUnits
        }

        protected override void RegisterEvaluationUnits(EvaluationUnitManager mngr)
        {
            // Register Isotropic Material
            EvaluationUnit isotropic = new EvaluationUnit(
                "Isotropic",
                "Isotropic Material",
                "Define isotropic material properties (same properties in all directions)"
            );
            // Common inputs
            isotropic.RegisterInputParam(
                new Param_String(),
                "Name",
                "Name",
                "Material name",
                GH_ParamAccess.item
            );
            isotropic.RegisterInputParam(
                new Param_Colour(),
                "Color",
                "Color",
                "Material color",
                GH_ParamAccess.item
            );
            isotropic.RegisterInputParam(
                new Param_Number(),
                "Density",
                "rho",
                "Density [kg/m³]",
                GH_ParamAccess.item
            );
            // Isotropic-specific inputs
            isotropic.RegisterInputParam(
                new Param_Number(),
                "Young's Modulus",
                "E",
                "Young's Modulus [Pa]",
                GH_ParamAccess.item
            );
            isotropic.RegisterInputParam(
                new Param_Number(),
                "Poisson's Ratio",
                "nu",
                "Poisson's Ratio [-]",
                GH_ParamAccess.item
            );
            // Output
            isotropic.RegisterOutputParam(
                new Param_GenericObject(),
                "Material",
                "M",
                "Material definition"
            );
            
            mngr.RegisterUnit(isotropic);

            // Register Orthotropic Material
            EvaluationUnit orthotropic = new EvaluationUnit(
                "Orthotropic",
                "Orthotropic Material",
                "Define orthotropic material properties (different properties in orthogonal directions)"
            );
            // Common inputs
            orthotropic.RegisterInputParam(
                new Param_String(),
                "Name",
                "Name",
                "Material name",
                GH_ParamAccess.item
            );
            orthotropic.RegisterInputParam(
                new Param_Colour(),
                "Color",
                "Color",
                "Material color",
                GH_ParamAccess.item
            );
            orthotropic.RegisterInputParam(
                new Param_Number(),
                "Density",
                "rho",
                "Density [kg/m³]",
                GH_ParamAccess.item
            );
            // Orthotropic-specific inputs
            orthotropic.RegisterInputParam(new Param_Number(), "E1", "E1", "Young's Modulus E1 [Pa]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "E2", "E2", "Young's Modulus E2 [Pa]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "E3", "E3", "Young's Modulus E3 [Pa]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "nu12", "nu12", "Poisson's Ratio nu12 [-]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "nu13", "nu13", "Poisson's Ratio nu13 [-]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "nu23", "nu23", "Poisson's Ratio nu23 [-]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "G12", "G12", "Shear Modulus G12 [Pa]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "G13", "G13", "Shear Modulus G13 [Pa]", GH_ParamAccess.item);
            orthotropic.RegisterInputParam(new Param_Number(), "G23", "G23", "Shear Modulus G23 [Pa]", GH_ParamAccess.item);
            // Output
            orthotropic.RegisterOutputParam(
                new Param_GenericObject(),
                "Material",
                "M",
                "Material definition"
            );
            
            mngr.RegisterUnit(orthotropic);

            // Register Stiffness Matrix Material
            EvaluationUnit stiffnessMatrix = new EvaluationUnit(
                "Stiffness Matrix",
                "Stiffness Matrix",
                "Define material using a full stiffness matrix"
            );
            // Common inputs (specific matrix inputs to be defined later)
            stiffnessMatrix.RegisterInputParam(new Param_String(), "Name", "Name", "Material name", GH_ParamAccess.item);
            stiffnessMatrix.RegisterInputParam(new Param_Colour(), "Color", "Color", "Material color", GH_ParamAccess.item);
            stiffnessMatrix.RegisterInputParam(new Param_Number(), "Density", "rho", "Density [kg/m³]", GH_ParamAccess.item);
            // Output
            stiffnessMatrix.RegisterOutputParam(
                new Param_GenericObject(),
                "Material",
                "M",
                "Material definition"
            );
            
            mngr.RegisterUnit(stiffnessMatrix);

            // Register Spring Material
            EvaluationUnit spring = new EvaluationUnit(
                "Spring",
                "Spring Material",
                "Define spring material properties"
            );
            // Common inputs
            spring.RegisterInputParam(new Param_String(), "Name", "Name", "Material name", GH_ParamAccess.item);
            spring.RegisterInputParam(new Param_Colour(), "Color", "Color", "Material color", GH_ParamAccess.item);
            spring.RegisterInputParam(new Param_Number(), "Density", "rho", "Density [kg/m³]", GH_ParamAccess.item);
            // Spring-specific inputs
            spring.RegisterInputParam(new Param_Number(), "Spring Constant", "k", "Spring constant [N/m]", GH_ParamAccess.item);
            // Output
            spring.RegisterOutputParam(
                new Param_GenericObject(),
                "Material",
                "M",
                "Material definition"
            );
            
            mngr.RegisterUnit(spring);
        }

        protected override void SolveInstance(IGH_DataAccess DA, EvaluationUnit unit)
        {
            string materialName = string.Empty;
            System.Drawing.Color color = System.Drawing.Color.Gray;
            double density = 0.0;

            // Common inputs for all units
            if (!DA.GetData(0, ref materialName))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Material name is required");
                return;
            }
            DA.GetData(1, ref color);
            DA.GetData(2, ref density);

            // Create material object based on the active unit
            MaterialBase material = null;
            
            switch (unit.Name)
            {
                case "Isotropic":
                {
                    double E = 0.0;
                    double nu = 0.0;
                    DA.GetData(3, ref E);
                    DA.GetData(4, ref nu);
                    var m = new IsotropicMaterial(materialName)
                    {
                        Color = color,
                        Density = density,
                        YoungModulus = E,
                        PoissonRatio = nu
                    };
                    material = m;
                    break;
                }
                case "Orthotropic":
                {
                    double E1 = 0.0, E2 = 0.0, E3 = 0.0;
                    double nu12 = 0.0, nu13 = 0.0, nu23 = 0.0;
                    double G12 = 0.0, G13 = 0.0, G23 = 0.0;
                    DA.GetData(3, ref E1);
                    DA.GetData(4, ref E2);
                    DA.GetData(5, ref E3);
                    DA.GetData(6, ref nu12);
                    DA.GetData(7, ref nu13);
                    DA.GetData(8, ref nu23);
                    DA.GetData(9, ref G12);
                    DA.GetData(10, ref G13);
                    DA.GetData(11, ref G23);
                    var m = new OrthotropicMaterial(materialName)
                    {
                        Color = color,
                        Density = density,
                        E1 = E1,
                        E2 = E2,
                        E3 = E3,
                        Nu12 = nu12,
                        Nu13 = nu13,
                        Nu23 = nu23,
                        G12 = G12,
                        G13 = G13,
                        G23 = G23
                    };
                    material = m;
                    break;
                }
                case "Stiffness Matrix":
                {
                    var m = new StiffnessMatrixMaterial(materialName)
                    {
                        Color = color,
                        Density = density
                    };
                    material = m;
                    break;
                }
                case "Spring":
                {
                    double k = 0.0;
                    DA.GetData(3, ref k);
                    var m = new SpringMaterial(materialName)
                    {
                        Color = color,
                        Density = density,
                        SpringConstant = k
                    };
                    material = m;
                    break;
                }
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown material type");
                    return;
            }

            DA.SetData(0, material);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // TODO: Add a proper icon
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D"); }
        }
    }
}

