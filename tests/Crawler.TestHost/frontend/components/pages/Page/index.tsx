import pages from '../../../../Response/default.json'
import { PageComponent } from '../../../types/PageComponent';
import { PageContent } from '../../../types/PageContent';
import { Container, Title, Description, Wrapper, Nav } from '../Shared/styles';

function Result({ page }: PageComponent) {
    const content = page.content as PageContent;
    return (
        <Wrapper color={page.color}>
            <Container>
                <>
                    <Title dangerouslySetInnerHTML={{__html: content.title}} />
                    <Description dangerouslySetInnerHTML={{__html: content.body}} />
                    <Nav>
                        {pages.map((x, i) => 
                            <li key={i}><a href={x.href}>{x.name}</a></li>
                        )}
                    </Nav>
                </>
            </Container>
        </Wrapper>
    );
}

export default Result;
