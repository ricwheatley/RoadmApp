import React from 'react';

export default function LastUpdate({ timestamp }) {
  if (!timestamp) {
    return <span>Not updated yet</span>;
  }

  const date = timestamp instanceof Date ? timestamp : new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return <span>Not updated yet</span>;
  }

  return <span>{date.toLocaleString()}</span>;
}
