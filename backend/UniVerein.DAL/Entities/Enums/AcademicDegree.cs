using System.ComponentModel.DataAnnotations;

namespace UniVerein.DAL.Entities.Enums;

public enum AcademicDegree
{
    [Display(Name = "B.A.")] 
    BA,

    [Display(Name = "B.Sc.")] 
    BSC,

    [Display(Name = "B.Eng.")] 
    BENG,

    [Display(Name = "LL.B.")]
    LLB,

    [Display(Name = "B.Ed.")]
    BED,

    [Display(Name = "BBA")]
    BBA,

    [Display(Name = "B.F.A.")] 
    BFA,

    [Display(Name = "B.Mus.")] 
    BMUS,

    [Display(Name = "B.Arch.")] 
    BARCH,

    [Display(Name = "B.N.")] 
    BN,

    [Display(Name = "B.S.W.")]
    BSW,

    [Display(Name = "B.Th.")] 
    BTH,

    [Display(Name = "B.Phil.")] 
    BPHIL,

    [Display(Name = "B.C.S.")] 
    BCS,

    [Display(Name = "B.Ec.")] 
    BEC,

    [Display(Name = "M.A.")] 
    MA,

    [Display(Name = "M.Sc.")]
    MSC,

    [Display(Name = "M.Eng.")] 
    MENG,

    [Display(Name = "LL.M.")] 
    LLM,

    [Display(Name = "M.Ed.")]
    MED,

    [Display(Name = "MBA")] 
    MBA,

    [Display(Name = "M.F.A.")] 
    MFA,

    [Display(Name = "M.Mus.")] 
    MMUS,

    [Display(Name = "M.Arch.")] 
    MARCH,

    [Display(Name = "MPH")] 
    MPH,

    [Display(Name = "M.S.W.")]
    MSW,

    [Display(Name = "MPA")] 
    MPA,

    [Display(Name = "M.Phil.")] 
    MPHIL,

    [Display(Name = "M.Th.")] 
    MTH,

    [Display(Name = "M.C.S.")]
    MCS,

    [Display(Name = "M.Ec.")] 
    MEC,

    [Display(Name = "M.Fin.")] 
    MFIN,

    [Display(Name = "M.I.R.")] 
    MIR,

    [Display(Name = "M.Res.")] 
    MRES,

    [Display(Name = "Ph.D.")] 
    PHD,

    [Display(Name = "M.D.")] 
    MD,

    [Display(Name = "LL.D.")] 
    LLD,

    [Display(Name = "D.Sc.")] 
    DSC,

    [Display(Name = "D.Eng.")]
    DENG,

    [Display(Name = "Ed.D.")]
    EDD,

    [Display(Name = "DBA")] 
    DBA,

    [Display(Name = "D.Th.")] 
    DTH,

    [Display(Name = "D.F.A.")] 
    DFA,

    [Display(Name = "D.Mus.")] 
    DMUS,

    [Display(Name = "Dr.P.H.")]
    DRPH,

    [Display(Name = "Psy.D.")] 
    PSYD,

    [Display(Name = "D.Arch.")] 
    DARCH,

    [Display(Name = "DNP")] 
    DNP,

    [Display(Name = "D.S.W.")] 
    DSW,

    [Display(Name = "J.D.")]
    JD,

    [Display(Name = "Dr.")] 
    DR,

    [Display(Name = "Habil.")] 
    HABIL,

    [Display(Name = "Dr. habil.")] 
    DRHABIL,

    [Display(Name = "Dr. h.c.")] 
    DRHC,

    [Display(Name = "Dr. h.c. mult.")] 
    DRHCMULT,

    [Display(Name = "Diplom")] 
    DIPLOM,

    [Display(Name = "Magister")] 
    MAGISTER,

    [Display(Name = "Staatsexamen")] 
    STAATSEXAMEN,

    [Display(Name = "Licence")] 
    LICENCE,

    [Display(Name = "Maîtrise")]
    MAITRISE,

    [Display(Name = "Ingénieur")] 
    INGENIEUR,

    [Display(Name = "Laurea")]
    LAUREA,

    [Display(Name = "Laurea Magistrale")] 
    LAUREAMAGISTRALE,

    [Display(Name = "Licenciatura")] 
    LICENCIATURA,

    [Display(Name = "Título de Grado")]
    TITULODEGRADO,

    [Display(Name = "Kandidát věd")] 
    KANDIDATVIED,

    [Display(Name = "Docent")] 
    DOCENT
}