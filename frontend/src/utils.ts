import { AcademicDegree, TaskWithinTheClub } from './types';
import { jwtDecode, JwtPayload } from 'jwt-decode';

export function formatIBAN(value: string) {
  return value
    .replace(/\s+/g, '')
    .replace(/(.{4})/g, '$1 ')
    .trim();
}

export function formatBIC(value: string) {
  return value
    .replace(/\s+/g, '')
    .toUpperCase()
    .replace(/^(.{4})(.{2})(.{2})/, '$1 $2 $3 ')
    .trim();
}

export function validateIBAN(iban: string) {
  const clean = iban.replace(/\s+/g, '').toUpperCase();
  console.log(clean);
  if (clean.length < 15 || clean.length > 34) return false;

  const rearranged = clean.slice(4) + clean.slice(0, 4);

  const numeric = rearranged.replace(/[A-Z]/g, (c) => (c.charCodeAt(0) - 55).toString());

  let remainder = numeric;
  while (remainder.length > 2) {
    const block = remainder.slice(0, 9);
    remainder = (parseInt(block, 10) % 97) + remainder.slice(block.length);
  }

  const result = parseInt(remainder, 10) % 97 === 1;
  console.log(result);
  return result;
}

export function validateBIC(bic: string) {
  const clean = bic.replace(/\s+/g, '').toUpperCase();

  if (clean.length !== 8 && clean.length !== 11) {
    return false;
  }

  const bicRegex = /^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$/;

  return bicRegex.test(clean);
}

export const isTokenValid = (token: string) => {
  if (!token) return false;

  try {
    const decoded: JwtPayload = jwtDecode(token);
    if (decoded.exp == undefined) return false;

    return decoded.exp > Date.now() / 1000;
  } catch (error) {
    return false;
  }
};

export const TASK_WITHIN_THE_CLUB_LABELS: Record<TaskWithinTheClub, string> = {
  [TaskWithinTheClub.MEMBER]: 'member',
  [TaskWithinTheClub.CHAIRMAN]: 'chairman',
  [TaskWithinTheClub.SECOND_CHAIRMAN]: 'secondChairman',
  [TaskWithinTheClub.JUNIOR_BOARD_MEMBER]: 'juniorBoardMember',
  [TaskWithinTheClub.CHIEF_FINANCE_OFFICER]: 'ChiefFinanceOfficer',
  [TaskWithinTheClub.WEBSITE_MANAGER]: 'websiteManager',
  [TaskWithinTheClub.ALUMNI_OFFICER]: 'alumniOfficer',
  [TaskWithinTheClub.STUDENT_COUNCIL_REPRESENTATIVE]: 'studentCouncilRepresentative',
};

export const ACADEMIC_DEGREE_LABELS: Record<AcademicDegree, string> = {
  // Bachelor's Level
  [AcademicDegree.BA]: 'B.A.',
  [AcademicDegree.BSC]: 'B.Sc.',
  [AcademicDegree.BENG]: 'B.Eng.',
  [AcademicDegree.LLB]: 'LL.B.',
  [AcademicDegree.BED]: 'B.Ed.',
  [AcademicDegree.BBA]: 'BBA',
  [AcademicDegree.BFA]: 'B.F.A.',
  [AcademicDegree.BMUS]: 'B.Mus.',
  [AcademicDegree.BARCH]: 'B.Arch.',
  [AcademicDegree.BN]: 'B.N.',
  [AcademicDegree.BSW]: 'B.S.W.',
  [AcademicDegree.BTH]: 'B.Th.',
  [AcademicDegree.BPHIL]: 'B.Phil.',
  [AcademicDegree.BCS]: 'B.C.S.',
  [AcademicDegree.BEC]: 'B.Ec.',
  // Master's Level
  [AcademicDegree.MA]: 'M.A.',
  [AcademicDegree.MSC]: 'M.Sc.',
  [AcademicDegree.MENG]: 'M.Eng.',
  [AcademicDegree.LLM]: 'LL.M.',
  [AcademicDegree.MED]: 'M.Ed.',
  [AcademicDegree.MBA]: 'MBA',
  [AcademicDegree.MFA]: 'M.F.A.',
  [AcademicDegree.MMUS]: 'M.Mus.',
  [AcademicDegree.MARCH]: 'M.Arch.',
  [AcademicDegree.MPH]: 'MPH',
  [AcademicDegree.MSW]: 'M.S.W.',
  [AcademicDegree.MPA]: 'MPA',
  [AcademicDegree.MPHIL]: 'M.Phil.',
  [AcademicDegree.MTH]: 'M.Th.',
  [AcademicDegree.MCS]: 'M.C.S.',
  [AcademicDegree.MEC]: 'M.Ec.',
  [AcademicDegree.MFIN]: 'M.Fin.',
  [AcademicDegree.MIR]: 'M.I.R.',
  [AcademicDegree.MRES]: 'M.Res.',
  // Doctoral Level
  [AcademicDegree.PHD]: 'Ph.D.',
  [AcademicDegree.MD]: 'M.D.',
  [AcademicDegree.LLD]: 'LL.D.',
  [AcademicDegree.DSC]: 'D.Sc.',
  [AcademicDegree.DENG]: 'D.Eng.',
  [AcademicDegree.EDD]: 'Ed.D.',
  [AcademicDegree.DBA]: 'DBA',
  [AcademicDegree.DTH]: 'D.Th.',
  [AcademicDegree.DFA]: 'D.F.A.',
  [AcademicDegree.DMUS]: 'D.Mus.',
  [AcademicDegree.DRPH]: 'Dr.P.H.',
  [AcademicDegree.PSYD]: 'Psy.D.',
  [AcademicDegree.DARCH]: 'D.Arch.',
  [AcademicDegree.DNP]: 'DNP',
  [AcademicDegree.DSW]: 'D.S.W.',
  [AcademicDegree.JD]: 'J.D.',
  [AcademicDegree.DR]: 'Dr.',
  // Post-Doctoral / Habilitation
  [AcademicDegree.HABIL]: 'Habil.',
  [AcademicDegree.DRHABIL]: 'Dr. habil.',
  // Honorary Degrees
  [AcademicDegree.DRHC]: 'Dr. h.c.',
  [AcademicDegree.DRHCMULT]: 'Dr. h.c. mult.',
  // Pre-Bologna / National Degrees
  [AcademicDegree.DIPLOM]: 'Diplom',
  [AcademicDegree.MAGISTER]: 'Magister',
  [AcademicDegree.STAATSEXAMEN]: 'Staatsexamen',
  [AcademicDegree.LICENCE]: 'Licence',
  [AcademicDegree.MAITRISE]: 'Maîtrise',
  [AcademicDegree.INGENIEUR]: 'Ingénieur',
  [AcademicDegree.LAUREA]: 'Laurea',
  [AcademicDegree.LAUREAMAGISTRALE]: 'Laurea Magistrale',
  [AcademicDegree.LICENCIATURA]: 'Licenciatura',
  [AcademicDegree.TITULODEGRADO]: 'Título de Grado',
  [AcademicDegree.KANDIDATVIED]: 'Kandidát věd',
  [AcademicDegree.DOCENT]: 'Docent',
};
