import styled from 'styled-components';
import { FontSpec } from "chart.js";

export const chartLabelStyle: Partial<FontSpec> = {
    size: 16, weight: 500, family: 'Raleway'
}

export const Container = styled.nav`
  margin: auto;
  max-width: 640px;
  width: 100%;
`;

interface WrapperProps {
    color: string;
}

export const Wrapper = styled.div<WrapperProps>`
  background-color: ${({ color }) => color};
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow-x: hidden;
  overflow-y: auto;
  padding: 24px 24px;
  width: 100%;
`;

export const Title = styled.h1`
  font-size: 2.2rem;
  font-weight: 300;
  font-smooth: always;
  margin-bottom: 24px;
  max-width: 720px;
  text-align: center;
  text-wrap: balance;
  letter-spacing: 1.1px;
  margin: 10px auto 40px;


  em {
    color: #eee;
    font-size: 4rem;
    letter-spacing: 0px;
  }
`;

export const Subtitle = styled.h3`
  font-size: 1.6rem;
  font-weight: 400;
  letter-spacing: 0.6px;
  line-height: 1.4;
  margin-bottom: 10px;
  margin-top: 20px;
  text-align: center;
`;

export const Description = styled.p`
  font-size: 1.2rem;
  line-height: 1.4;
  margin-bottom: 48px;
  text-align: center;
`;

export const Nav = styled.ul`
  list-style-type: none;
  display: inline-flex;
  gap: 10px;
  flex-wrap: nowrap;
  
  li {
    display:inline-block;
    padding: 0.75rem 1rem;
    background-color: #0003;
    border-radius: 5px;
    height: 0%;
    white-space: nowrap;
  }
`;