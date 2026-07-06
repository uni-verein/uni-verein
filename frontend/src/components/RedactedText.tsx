import React from 'react';

export function RedactedText({ text }: { text: string }) {
  return (
    <span>
      {text.split(/@@/).map((part, i, arr) => (
        <React.Fragment key={i}>
          {part}
          {i < arr.length - 1 && (
            <span
              style={{
                display: 'inline-block',
                width: '3em',
                height: '1em',
                backgroundColor: '#000',
              }}
            />
          )}
        </React.Fragment>
      ))}
    </span>
  );
}
