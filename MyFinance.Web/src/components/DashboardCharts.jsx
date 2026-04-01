import React, { useMemo } from 'react';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
} from 'chart.js';
import { Bar } from 'react-chartjs-2';

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend);

export default function DashboardCharts({ categorySummary = [], compact = false }) {
  const chartData = useMemo(() => {
    return {
      labels: categorySummary.map((item) => item.name),
      datasets: [
        {
          label: 'Despesas (R$)',
          data: categorySummary.map((item) => item.total),
          backgroundColor: 'rgba(24, 144, 255, 0.6)',
          borderColor: 'rgba(24, 144, 255, 1)',
          borderWidth: 1,
          borderRadius: 4,
        },
      ],
    };
  }, [categorySummary]);

  const options = useMemo(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: compact ? 'top' : 'bottom' },
        title: { display: false },
      },
      scales: {
        y: { beginAtZero: true },
        x: {
          grid: { display: false },
          ticks: {
            autoSkip: true,
            maxRotation: compact ? 35 : 0,
            minRotation: compact ? 25 : 0,
          },
        },
      },
    }),
    [compact],
  );

  return (
    <div style={{ height: compact ? 260 : 300 }}>
      <Bar options={options} data={chartData} />
    </div>
  );
}
