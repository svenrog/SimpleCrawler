import { PageComponent } from '../../../types/PageComponent';
import { Container, Description, Title, Wrapper } from '../Shared/styles';

function NotFound({ }: PageComponent) {
    return (
        <Wrapper color="#222">
            <Container>
                <Title>Oops, it looks like this page is <em>missing</em></Title>
                <Description>Try something else.</Description>
            </Container>
        </Wrapper>
    );
}

export default NotFound;
