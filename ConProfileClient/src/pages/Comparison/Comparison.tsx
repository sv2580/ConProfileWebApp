// Comparison.tsx
import React, { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import CloseIcon from '@mui/icons-material/Close';
import './index.css';
import { FolderDTO } from '../../types';
import { ScatterChart } from '@mui/x-charts/ScatterChart';
import { Checkbox, IconButton, ListItem, ListItemText, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material';

import List from '@mui/material/List/List';

interface ComparisonProps {
  open: boolean;
  onClose: () => void;
  folders: FolderDTO[] | null;
}

interface ChartData {
  data: number[];
  label: string;
}


interface StatData {
  max: number;
  min: number;
  std: number;
  folderName: string;
}

const Comparison: React.FC<ComparisonProps> = ({ open, onClose, folders }) => {
  const [checked, setChecked] = useState<number[]>([]);
  const [chartData, setChartData] = useState<ChartData[] | null>(null);
  const [statData, setStatData] = useState<StatData[]>([]);

  useEffect(() => {
    //nacitanie legendy && statistik
    console.log(checked)
  }, [checked]);

  const handleChange = (event: React.ChangeEvent<HTMLInputElement>, id: number) => {
    console.log(event.target);
    const list: number[] = checked;
    if (!checked.includes(id)) {
      list.push(id);
      setChecked(list);
      if (list.length >= 2) {
        const filteredFolders = folders?.filter(data => checked.includes(data.id))
          .map((data) => ({
            data: data?.profile || [],
            label: data?.foldername || 'Unknown',
          })) || [];
        setChartData(filteredFolders);

        const statList: StatData[] = [];
        filteredFolders.forEach(element => {
          const multipliedMax: number = Math.max(...element.data);
          const multipliedMin: number = Math.min(...element.data);
          const mean = element.data.reduce((sum, number) => sum + number, 0) / element.data.length;
          const squaredDifferences = element.data.map(number => Math.pow(number - mean, 2));
          const variance = squaredDifferences.reduce((sum, squaredDifference) => sum + squaredDifference, 0) / element.data.length;
          const multipliedStandardDeviation = Math.sqrt(variance);
          const statistics: StatData = { max: multipliedMax, min: multipliedMin, std: multipliedStandardDeviation, folderName: element.label };
          statList.push(statistics)
        });
        setStatData(statList);
      }
    }
  };

  return (
    <Dialog
      onClose={onClose}
      aria-labelledby="customized-dialog-title"
      open={open}
      maxWidth='md'
    >
      <DialogTitle sx={{ m: 0, p: 2 }} id="customized-dialog-title">
        Porovnanie profilov
      </DialogTitle>
      <IconButton
        aria-label="close"
        onClick={onClose}
        sx={{
          position: 'absolute',
          right: 8,
          top: 8,
          color: (theme) => theme.palette.grey[500],
        }}
      >
        <CloseIcon />
      </IconButton>
      <DialogContent dividers>
        <div className='dialog-content' >
          <div style={{ display: 'flex', flexDirection: 'row', minHeight: '100%' }}>
            <div style={{ width: '40%' }}>
              <div className='checkboxlitwindow'>
                <List dense={true}>
                  {folders?.map((value) => (
                    <ListItem style={{ padding: '0px' }}>
                      <Checkbox
                        onChange={(event) => handleChange(event, value.id)}
                        inputProps={{ 'id': '${value.id}' }}
                        style={{ paddingTop: '0px', paddingBottom: '0px', paddingLeft: '2px', paddingRight: '2px' }}
                      />
                      <ListItemText
                        primary={value.foldername}
                      />
                    </ListItem>
                  ))}

                </List>

              </div>

              {chartData && statData && chartData.length >= 2 ?
                <div style={{ width: '100%', height: '40%', overflow: 'auto' }}> <TableContainer component={Paper} >

                  <Table sx={{ width: '100%' }} stickyHeader size="small" aria-label="a dense table">
                    <TableHead>
                      <TableRow >
                        <TableCell style={{ fontFamily: 'Poppins', fontWeight: 'bolder' }}> Priečinok</TableCell >
                        <TableCell style={{ fontFamily: 'Poppins', fontWeight: 'bolder' }}> Max</TableCell >
                        <TableCell style={{ fontFamily: 'Poppins', fontWeight: 'bolder' }}> Min</TableCell >
                        <TableCell style={{ fontFamily: 'Poppins', fontWeight: 'bolder' }}> Std</TableCell >
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {statData.map((stat: StatData) => {
                        return (<TableRow>
                          <TableCell> {stat.folderName} </TableCell>
                          <TableCell> {stat.max.toFixed(5)} </TableCell>
                          <TableCell> {stat.min.toFixed(5)} </TableCell>
                          <TableCell> {stat.std.toFixed(5)} </TableCell>
                        </TableRow>);
                      })}
                    </TableBody>
                  </Table>
                </TableContainer></div> : ""}
            </div>
            <div style={{ width: '60%' }}>
              {chartData && folders ?
                <div style={{ height: '50vh', margin: '10px', backgroundColor: 'white' }}>
                  <ScatterChart
                    series={chartData.map((data) => ({
                      label: data.label,
                      data: data.data.map((v, index) => ({ x: folders[0].excitation[index], y: v, id: v })),
                    }))}
                    yAxis={[{ min: 0 }]}
                    xAxis={[{ min: 250 }]}
                  />
                </div>
                : ""}
            </div>
          </div>
        </div>
      </DialogContent>

    </Dialog>
  );
};

export default Comparison;
