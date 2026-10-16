import { useCallback, useState } from 'react';
import dayjs from 'dayjs';

interface Occurrence {
  end: string;
  allDay: boolean;
}

function isPast(o: Occurrence): boolean {
  if (o.allDay) return dayjs(o.end.substring(0, 10)).valueOf() <= dayjs().startOf('day').valueOf();
  return new Date(o.end).getTime() < Date.now();
}

export function usePastEvents() {
  const [showPast, setShowPast] = useState(false);
  const [pastCount, setPastCount] = useState(0);

  const hidePast = useCallback(
    <T extends Occurrence>(
      range: { start: Date; end: Date },
      viewType: string | undefined,
      items: T[],
    ): T[] => {
      const now = Date.now();
      const isCurrentMonthList =
        viewType === 'listMonth' && range.start.getTime() <= now && now < range.end.getTime();
      const past = isCurrentMonthList ? items.filter(isPast) : [];
      setPastCount(past.length);
      return showPast || past.length === 0 ? items : items.filter((o) => !past.includes(o));
    },
    [showPast],
  );

  const toggle = useCallback(() => setShowPast((v) => !v), []);

  return { showPast, pastCount, hidePast, toggle };
}
