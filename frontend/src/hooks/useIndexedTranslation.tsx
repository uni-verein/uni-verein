import { useTranslation } from 'react-i18next';

export const useIndexedTranslation = () => {
  const { t } = useTranslation();

  const ti = (key: string, values?: (string | number)[]): string => {
    if (!values || values.length === 0) return t(key);

    const interpolationMap = values.reduce<Record<string, string | number>>((acc, value, index) => {
      acc[index] = value;
      return acc;
    }, {});

    return t(key, interpolationMap);
  };

  return { ti, t };
};
