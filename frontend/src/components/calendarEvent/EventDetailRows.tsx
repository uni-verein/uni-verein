import React from 'react';
import { Box, Link, Typography } from '@mui/material';
import PlaceIcon from '@mui/icons-material/Place';
import LinkIcon from '@mui/icons-material/Link';
import { mapSearchUrl } from '../../utils/calendar';

export function DetailRow({
  icon,
  alignItems = 'flex-start',
  children,
}: {
  icon: React.ReactNode;
  alignItems?: 'flex-start' | 'center';
  children: React.ReactNode;
}) {
  return (
    <Box sx={{ display: 'flex', gap: 1.5, alignItems, minWidth: 0 }}>
      {icon}
      {children}
    </Box>
  );
}

export function EventLocationRows({
  locationName,
  locationAddress,
  locationUrl,
}: {
  locationName: string;
  locationAddress: string;
  locationUrl: string;
}) {
  return (
    <>
      {(locationName || locationAddress) && (
        <DetailRow icon={<PlaceIcon color="action" />}>
          <Box sx={{ minWidth: 0 }}>
            {locationName && <Typography>{locationName}</Typography>}
            {locationAddress && (
              <Link href={mapSearchUrl(locationAddress)} target="_blank" rel="noopener noreferrer">
                {locationAddress}
              </Link>
            )}
          </Box>
        </DetailRow>
      )}

      {locationUrl && (
        <DetailRow icon={<LinkIcon color="action" />} alignItems="center">
          <Link
            href={locationUrl}
            target="_blank"
            rel="noopener noreferrer"
            sx={{ overflowWrap: 'anywhere' }}
          >
            {locationUrl}
          </Link>
        </DetailRow>
      )}
    </>
  );
}
